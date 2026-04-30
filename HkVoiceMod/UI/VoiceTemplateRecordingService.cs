using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using HkVoiceMod.Commands;
using NAudio.Wave;

namespace HkVoiceMod.UI
{
    internal sealed class VoiceTemplateRecordingService : IDisposable
    {
        private readonly object _sync = new object();
        private readonly List<byte[]> _buffers = new List<byte[]>();

        private WaveInEvent? _waveInEvent;
        private ManualResetEventSlim? _stoppedSignal;
        private bool _disposed;

        public bool IsRecording { get; private set; }

        public TemplateRecordingResult? LastResult { get; private set; }

        public void Start(VoiceModSettings settings)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(VoiceTemplateRecordingService));
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                throw new InvalidOperationException("模板录音当前仅支持 Windows。");
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            StopInternal(false);
            LastResult = null;
            lock (_sync)
            {
                _buffers.Clear();
            }

            _stoppedSignal = new ManualResetEventSlim(false);
            _waveInEvent = new WaveInEvent
            {
                DeviceNumber = 0,
                BufferMilliseconds = settings.CaptureBufferMilliseconds,
                NumberOfBuffers = 3,
                WaveFormat = new WaveFormat(settings.SampleRateHz, 16, 1)
            };

            _waveInEvent.DataAvailable += OnDataAvailable;
            _waveInEvent.RecordingStopped += OnRecordingStopped;
            _waveInEvent.StartRecording();
            IsRecording = true;
        }

        public TemplateRecordingResult Stop(VoiceModSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            StopInternal(true);
            var pcmBytes = MergeBuffers();
            var trimmed = TrimSilence(pcmBytes, settings.SampleRateHz, Math.Max(settings.VoiceActivityRmsThreshold, 0.003f));
            if (trimmed.Length == 0)
            {
                LastResult = new TemplateRecordingResult(Array.Empty<byte>(), settings.SampleRateHz, 0);
                return LastResult;
            }

            var durationMilliseconds = ResolveDurationMilliseconds(trimmed.Length, settings.SampleRateHz);
            LastResult = new TemplateRecordingResult(trimmed, settings.SampleRateHz, durationMilliseconds);
            return LastResult;
        }

        public void Cancel()
        {
            StopInternal(false);
            LastResult = null;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            StopInternal(false);
            _disposed = true;
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs args)
        {
            if (args.BytesRecorded <= 0)
            {
                return;
            }

            var copy = new byte[args.BytesRecorded];
            Buffer.BlockCopy(args.Buffer, 0, copy, 0, args.BytesRecorded);
            lock (_sync)
            {
                _buffers.Add(copy);
            }
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs args)
        {
            IsRecording = false;
            _stoppedSignal?.Set();
        }

        private void StopInternal(bool waitForStop)
        {
            if (_waveInEvent == null)
            {
                IsRecording = false;
                return;
            }

            try
            {
                _waveInEvent.StopRecording();
                if (waitForStop)
                {
                    _stoppedSignal?.Wait(TimeSpan.FromSeconds(3));
                }
            }
            finally
            {
                _waveInEvent.DataAvailable -= OnDataAvailable;
                _waveInEvent.RecordingStopped -= OnRecordingStopped;
                _waveInEvent.Dispose();
                _waveInEvent = null;
                _stoppedSignal?.Dispose();
                _stoppedSignal = null;
                IsRecording = false;
            }
        }

        private byte[] MergeBuffers()
        {
            lock (_sync)
            {
                var totalLength = 0;
                for (var index = 0; index < _buffers.Count; index++)
                {
                    totalLength += _buffers[index].Length;
                }

                var merged = new byte[totalLength];
                var offset = 0;
                for (var index = 0; index < _buffers.Count; index++)
                {
                    var buffer = _buffers[index];
                    Buffer.BlockCopy(buffer, 0, merged, offset, buffer.Length);
                    offset += buffer.Length;
                }

                _buffers.Clear();
                return merged;
            }
        }

        private static byte[] TrimSilence(byte[] pcmBytes, int sampleRateHz, float rmsThreshold)
        {
            if (pcmBytes.Length < 2)
            {
                return Array.Empty<byte>();
            }

            const int frameMilliseconds = 20;
            var frameSampleCount = Math.Max(1, sampleRateHz * frameMilliseconds / 1000);
            var totalSamples = pcmBytes.Length / 2;
            var firstVoicedSample = -1;
            var lastVoicedSample = -1;

            for (var frameStart = 0; frameStart < totalSamples; frameStart += frameSampleCount)
            {
                var frameEnd = Math.Min(totalSamples, frameStart + frameSampleCount);
                var rms = ComputeRms(pcmBytes, frameStart, frameEnd);
                if (rms < rmsThreshold)
                {
                    continue;
                }

                if (firstVoicedSample < 0)
                {
                    firstVoicedSample = frameStart;
                }

                lastVoicedSample = frameEnd;
            }

            if (firstVoicedSample < 0 || lastVoicedSample <= firstVoicedSample)
            {
                return Array.Empty<byte>();
            }

            var paddingSamples = Math.Max(1, sampleRateHz * 40 / 1000);
            firstVoicedSample = Math.Max(0, firstVoicedSample - paddingSamples);
            lastVoicedSample = Math.Min(totalSamples, lastVoicedSample + paddingSamples);

            var byteOffset = firstVoicedSample * 2;
            var byteCount = Math.Max(0, (lastVoicedSample - firstVoicedSample) * 2);
            var trimmed = new byte[byteCount];
            Buffer.BlockCopy(pcmBytes, byteOffset, trimmed, 0, byteCount);
            return trimmed;
        }

        private static float ComputeRms(byte[] pcmBytes, int sampleStart, int sampleEnd)
        {
            if (sampleEnd <= sampleStart)
            {
                return 0f;
            }

            double sumSquares = 0d;
            for (var sampleIndex = sampleStart; sampleIndex < sampleEnd; sampleIndex++)
            {
                var byteOffset = sampleIndex * 2;
                short sample = (short)(pcmBytes[byteOffset] | (pcmBytes[byteOffset + 1] << 8));
                var normalized = sample / 32768f;
                sumSquares += normalized * normalized;
            }

            return (float)Math.Sqrt(sumSquares / Math.Max(1, sampleEnd - sampleStart));
        }

        private static int ResolveDurationMilliseconds(int pcmByteLength, int sampleRateHz)
        {
            var sampleCount = pcmByteLength / 2;
            return (int)Math.Round(sampleCount * 1000d / sampleRateHz, MidpointRounding.AwayFromZero);
        }
    }

    internal sealed class TemplateRecordingResult
    {
        public TemplateRecordingResult(byte[] pcmBytes, int sampleRateHz, int durationMilliseconds)
        {
            PcmBytes = pcmBytes ?? Array.Empty<byte>();
            SampleRateHz = sampleRateHz;
            DurationMilliseconds = durationMilliseconds;
        }

        public byte[] PcmBytes { get; }

        public int SampleRateHz { get; }

        public int DurationMilliseconds { get; }

        public bool HasAudio => PcmBytes.Length > 0 && DurationMilliseconds > 0;
    }
}
