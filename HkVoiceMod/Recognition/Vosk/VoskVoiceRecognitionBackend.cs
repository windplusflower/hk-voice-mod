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
using Newtonsoft.Json.Linq;
using Vosk;

namespace HkVoiceMod.Recognition.Vosk
{
    public sealed class VoskVoiceRecognitionBackend : IVoiceRecognitionBackend
    {
        private static readonly IReadOnlyDictionary<string, VoiceCommand> CommandLookup = new Dictionary<string, VoiceCommand>
        {
            ["上"] = VoiceCommand.Up,
            ["下"] = VoiceCommand.Down,
            ["左"] = VoiceCommand.Left,
            ["右"] = VoiceCommand.Right,
            ["劈"] = VoiceCommand.Attack,
            ["跳"] = VoiceCommand.Jump,
            ["冲"] = VoiceCommand.Dash,
            ["吼"] = VoiceCommand.Howl,
            ["砸"] = VoiceCommand.Dive,
            ["波"] = VoiceCommand.Cast,
            ["停"] = VoiceCommand.Stop
        };

        private readonly VoiceModSettings _settings;
        private readonly Action<string> _logInfo;
        private readonly Action<string> _logWarn;
        private readonly Action<string> _logError;
        private readonly BlockingCollection<byte[]> _audioBuffers = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>());

        private ConcurrentQueue<RecognizedCommandEvent>? _outputQueue;
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _workerTask;
        private WaveInEvent? _waveInEvent;
        private DateTime _startedUtc;
        private bool _disposed;

        public VoskVoiceRecognitionBackend(
            VoiceModSettings settings,
            Action<string>? logInfo = null,
            Action<string>? logWarn = null,
            Action<string>? logError = null)
        {
            _settings = settings?.Clone() ?? new VoiceModSettings();
            _logInfo = logInfo ?? (_ => { });
            _logWarn = logWarn ?? (_ => { });
            _logError = logError ?? (_ => { });
        }

        public bool IsRunning => _workerTask != null && !_workerTask.IsCompleted;

        public void Start(ConcurrentQueue<RecognizedCommandEvent> outputQueue)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(VoskVoiceRecognitionBackend));
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
                var modelPath = _settings.ResolveModelPath(assemblyDirectory);
                if (!Directory.Exists(modelPath))
                {
                    throw new DirectoryNotFoundException($"Vosk model directory was not found: {modelPath}");
                }

                global::Vosk.Vosk.SetLogLevel(_settings.EnableVerboseLogging ? 0 : -1);

                using (var model = new Model(modelPath))
                using (var recognizer = new VoskRecognizer(model, _settings.SampleRateHz))
                {
                    recognizer.SetMaxAlternatives(0);
                    recognizer.SetWords(false);

                    using (var waveIn = CreateWaveIn())
                    {
                        _waveInEvent = waveIn;
                        waveIn.StartRecording();
                        _logInfo($"Vosk microphone loop started. Model={modelPath}");

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

                            ProcessBuffer(recognizer, buffer);
                        }

                        ProcessRecognitionResult(recognizer.FinalResult());
                    }
                }
            }
            catch (Exception ex)
            {
                _logError($"Vosk recognition loop crashed: {ex}");
            }
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

        private void ProcessBuffer(VoskRecognizer recognizer, byte[] buffer)
        {
            if (!recognizer.AcceptWaveform(buffer, buffer.Length))
            {
                return;
            }

            ProcessRecognitionResult(recognizer.Result());
        }

        private void ProcessRecognitionResult(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            string? text = null;
            try
            {
                var token = JObject.Parse(json);
                text = token.Value<string>("text");
            }
            catch (Exception ex)
            {
                _logWarn($"Failed to parse recognition payload '{json}': {ex.Message}");
                return;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (text == null)
            {
                return;
            }

            var normalized = NormalizeRecognizedText(text);
            if (!CommandLookup.TryGetValue(normalized, out var command))
            {
                _logWarn($"Ignored non-whitelisted recognition result: '{text}'");
                return;
            }

            _outputQueue?.Enqueue(new RecognizedCommandEvent(command, text, (float)(DateTime.UtcNow - _startedUtc).TotalSeconds));
        }

        private static string NormalizeRecognizedText(string text)
        {
            return text.Replace(" ", string.Empty).Trim();
        }
    }
}
