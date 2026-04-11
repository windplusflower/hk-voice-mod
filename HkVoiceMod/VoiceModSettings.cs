using System;
using System.IO;

namespace HkVoiceMod
{
    [Serializable]
    public sealed class VoiceModSettings
    {
        public bool Enabled { get; set; } = true;

        public string VoskModelPath { get; set; } = "assets/vosk-model-cn";

        public float ShortPressDurationSeconds { get; set; } = 0.08f;

        public float TimedHoldDurationSeconds { get; set; } = 0.5f;

        public int SampleRateHz { get; set; } = 16000;

        public int CaptureBufferMilliseconds { get; set; } = 50;

        public bool EnableVerboseLogging { get; set; } = true;

        public bool LogRecognizedText { get; set; } = true;

        public VoiceModSettings Clone()
        {
            return new VoiceModSettings
            {
                Enabled = Enabled,
                VoskModelPath = VoskModelPath,
                ShortPressDurationSeconds = ShortPressDurationSeconds,
                TimedHoldDurationSeconds = TimedHoldDurationSeconds,
                SampleRateHz = SampleRateHz,
                CaptureBufferMilliseconds = CaptureBufferMilliseconds,
                EnableVerboseLogging = EnableVerboseLogging,
                LogRecognizedText = LogRecognizedText
            };
        }

        public string ResolveModelPath(string assemblyDirectory)
        {
            if (Path.IsPathRooted(VoskModelPath))
            {
                return VoskModelPath;
            }

            return Path.GetFullPath(Path.Combine(assemblyDirectory, VoskModelPath));
        }
    }
}
