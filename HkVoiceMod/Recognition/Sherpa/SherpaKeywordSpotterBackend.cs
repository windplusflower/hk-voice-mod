using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using HkVoiceMod.Commands;
using NAudio.Wave;
using SherpaOnnx;

namespace HkVoiceMod.Recognition.Sherpa
{
    public sealed class SherpaKeywordSpotterBackend : IVoiceRecognitionBackend
    {
        private const int FeatureDim = 80;
        private const int KeywordSpotterThreads = 2;

        private static readonly IReadOnlyDictionary<string, VoiceCommand> CommandLookup = new Dictionary<string, VoiceCommand>
        {
            ["往上"] = VoiceCommand.Up,
            ["往下"] = VoiceCommand.Down,
            ["往左"] = VoiceCommand.Left,
            ["往右"] = VoiceCommand.Right,
            ["攻击"] = VoiceCommand.Attack,
            ["跳跃"] = VoiceCommand.Jump,
            ["冲刺"] = VoiceCommand.Dash,
            ["上吼"] = VoiceCommand.Howl,
            ["下砸"] = VoiceCommand.Dive,
            ["放波"] = VoiceCommand.Cast,
            ["停止"] = VoiceCommand.Stop
        };

        private static readonly string[] RequiredModelFiles =
        {
            "encoder.onnx",
            "decoder.onnx",
            "joiner.onnx",
            "tokens.txt",
            "keywords.txt"
        };

        private readonly VoiceModSettings _settings;
        private readonly Action<string> _logDebug;
        private readonly Action<string> _logInfo;
        private readonly Action<string> _logWarn;
        private readonly Action<string> _logError;
        private readonly BlockingCollection<byte[]> _audioBuffers = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>());
        private readonly Dictionary<VoiceCommand, DateTime> _lastEmittedCommands = new Dictionary<VoiceCommand, DateTime>();

        private ConcurrentQueue<RecognizedCommandEvent>? _outputQueue;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _workerTask;
        private WaveInEvent? _waveInEvent;
        private DateTime _startedUtc;
        private bool _disposed;

        public SherpaKeywordSpotterBackend(
            VoiceModSettings settings,
            Action<string>? logDebug = null,
            Action<string>? logInfo = null,
            Action<string>? logWarn = null,
            Action<string>? logError = null)
        {
            _settings = settings?.Clone() ?? new VoiceModSettings();
            _logDebug = logDebug ?? (_ => { });
            _logInfo = logInfo ?? (_ => { });
            _logWarn = logWarn ?? (_ => { });
            _logError = logError ?? (_ => { });
        }

        public bool IsRunning => _workerTask != null && !_workerTask.IsCompleted;

        public void Start(ConcurrentQueue<RecognizedCommandEvent> outputQueue)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SherpaKeywordSpotterBackend));
            }

            if (outputQueue == null)
            {
                throw new ArgumentNullException(nameof(outputQueue));
            }

            if (IsRunning)
            {
                return;
            }

            _outputQueue = outputQueue;
            _cancellationTokenSource = new CancellationTokenSource();
            _startedUtc = DateTime.UtcNow;
            _workerTask = Task.Run(() => RunRecognitionLoop(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
        }

        public void Stop()
        {
            if (_cancellationTokenSource == null)
            {
                return;
            }

            _cancellationTokenSource.Cancel();

            try
            {
                _waveInEvent?.StopRecording();
            }
            catch (Exception ex)
            {
                _logWarn($"StopRecording failed: {ex.Message}");
            }

            _audioBuffers.CompleteAdding();

            if (_workerTask != null)
            {
                try
                {
                    _workerTask.Wait(TimeSpan.FromSeconds(5));
                }
                catch (AggregateException ex)
                {
                    _logWarn($"Voice worker stopped with an exception: {ex.Flatten().InnerException?.Message ?? ex.Message}");
                }
            }

            _waveInEvent?.Dispose();
            _waveInEvent = null;
            _workerTask = null;
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop();
            _audioBuffers.Dispose();
            _disposed = true;
        }

        private void RunRecognitionLoop(CancellationToken cancellationToken)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logWarn("NAudio capture backend is currently enabled only on Windows. Startup skipped.");
                return;
            }

            try
            {
                var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;
                SherpaNativeLoader.EnsureLoaded(assemblyDirectory, _logInfo, _logWarn, _logError);
                var modelPath = _settings.ResolveModelPath(assemblyDirectory);
                ValidateModelFiles(modelPath);

                using (var keywordSpotter = new KeywordSpotter(CreateKeywordSpotterConfig(modelPath)))
                using (var stream = keywordSpotter.CreateStream())
                using (var waveIn = CreateWaveIn())
                {
                    _waveInEvent = waveIn;
                    waveIn.StartRecording();
                    _logInfo($"Sherpa keyword spotting started. Model={modelPath}");

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        byte[] buffer;
                        try
                        {
                            buffer = _audioBuffers.Take(cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (InvalidOperationException)
                        {
                            break;
                        }

                        ProcessBuffer(keywordSpotter, stream, buffer);
                    }

                    stream.InputFinished();
                    while (keywordSpotter.IsReady(stream))
                    {
                        keywordSpotter.Decode(stream);
                    }

                    ProcessKeywordResult(keywordSpotter, stream);
                }
            }
            catch (Exception ex)
            {
                _logError($"Sherpa keyword spotting loop crashed: {ex}");
            }
        }

        private KeywordSpotterConfig CreateKeywordSpotterConfig(string modelPath)
        {
            return new KeywordSpotterConfig
            {
                FeatConfig = new FeatureConfig
                {
                    SampleRate = _settings.SampleRateHz,
                    FeatureDim = FeatureDim
                },
                ModelConfig = new OnlineModelConfig
                {
                    Transducer = new OnlineTransducerModelConfig
                    {
                        Encoder = Path.Combine(modelPath, "encoder.onnx"),
                        Decoder = Path.Combine(modelPath, "decoder.onnx"),
                        Joiner = Path.Combine(modelPath, "joiner.onnx")
                    },
                    Tokens = Path.Combine(modelPath, "tokens.txt"),
                    NumThreads = KeywordSpotterThreads,
                    Provider = "cpu",
                    Debug = _settings.EnableVerboseLogging ? 1 : 0
                },
                MaxActivePaths = 4,
                NumTrailingBlanks = 1,
                KeywordsScore = 1.0f,
                KeywordsThreshold = 0.25f,
                KeywordsFile = Path.Combine(modelPath, "keywords.txt")
            };
        }

        private WaveInEvent CreateWaveIn()
        {
            var waveIn = new WaveInEvent
            {
                BufferMilliseconds = _settings.CaptureBufferMilliseconds,
                DeviceNumber = 0,
                WaveFormat = new WaveFormat(_settings.SampleRateHz, 16, 1),
                NumberOfBuffers = 3
            };

            waveIn.DataAvailable += OnDataAvailable;
            waveIn.RecordingStopped += OnRecordingStopped;
            return waveIn;
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs args)
        {
            if (_cancellationTokenSource == null || _cancellationTokenSource.IsCancellationRequested || args.BytesRecorded <= 0)
            {
                return;
            }

            var copy = new byte[args.BytesRecorded];
            Buffer.BlockCopy(args.Buffer, 0, copy, 0, args.BytesRecorded);

            if (!_audioBuffers.IsAddingCompleted)
            {
                _audioBuffers.Add(copy);
            }
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs args)
        {
            if (args.Exception != null)
            {
                _logWarn($"Microphone recording stopped with an exception: {args.Exception.Message}");
            }
        }

        private void ProcessBuffer(KeywordSpotter keywordSpotter, OnlineStream stream, byte[] buffer)
        {
            var samples = ConvertPcm16ToFloat(buffer);
            stream.AcceptWaveform(_settings.SampleRateHz, samples);

            while (keywordSpotter.IsReady(stream))
            {
                keywordSpotter.Decode(stream);
            }

            ProcessKeywordResult(keywordSpotter, stream);
        }

        private void ProcessKeywordResult(KeywordSpotter keywordSpotter, OnlineStream stream)
        {
            var result = keywordSpotter.GetResult(stream);
            var keyword = NormalizeKeyword(result.Keyword);
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return;
            }

            if (!CommandLookup.TryGetValue(keyword, out var command))
            {
                _logWarn($"Ignored non-whitelisted keyword result: '{keyword}'");
                keywordSpotter.Reset(stream);
                return;
            }

            if (ShouldSuppressDuplicate(command))
            {
                _logDebug($"Suppressed duplicate keyword '{keyword}' during cooldown window.");
                keywordSpotter.Reset(stream);
                return;
            }

            _outputQueue?.Enqueue(new RecognizedCommandEvent(command, keyword, (float)(DateTime.UtcNow - _startedUtc).TotalSeconds));
            keywordSpotter.Reset(stream);
        }

        private bool ShouldSuppressDuplicate(VoiceCommand command)
        {
            var cooldown = TimeSpan.FromMilliseconds(Math.Max(0, _settings.DuplicateCommandCooldownMilliseconds));
            var now = DateTime.UtcNow;

            if (cooldown > TimeSpan.Zero && _lastEmittedCommands.TryGetValue(command, out var previousEmission) && now - previousEmission < cooldown)
            {
                return true;
            }

            _lastEmittedCommands[command] = now;
            return false;
        }

        private static float[] ConvertPcm16ToFloat(byte[] buffer)
        {
            var sampleCount = buffer.Length / 2;
            var samples = new float[sampleCount];

            for (var i = 0; i < sampleCount; i++)
            {
                var offset = i * 2;
                short sample = (short)(buffer[offset] | (buffer[offset + 1] << 8));
                samples[i] = sample / 32768f;
            }

            return samples;
        }

        private static string NormalizeKeyword(string keyword)
        {
            return (keyword ?? string.Empty).Replace(" ", string.Empty).Trim().TrimStart('@');
        }

        private static void ValidateModelFiles(string modelPath)
        {
            if (!Directory.Exists(modelPath))
            {
                throw new DirectoryNotFoundException($"Sherpa model directory was not found: {modelPath}");
            }

            var missingFiles = new List<string>();
            foreach (var fileName in RequiredModelFiles)
            {
                var fullPath = Path.Combine(modelPath, fileName);
                if (!File.Exists(fullPath))
                {
                    missingFiles.Add(fullPath);
                }
            }

            if (missingFiles.Count > 0)
            {
                throw new FileNotFoundException($"Sherpa keyword spotting model is missing required files: {string.Join(", ", missingFiles)}");
            }
        }
    }
}
