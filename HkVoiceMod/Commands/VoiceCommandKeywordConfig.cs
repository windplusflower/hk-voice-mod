using System;

namespace HkVoiceMod.Commands
{
    [Serializable]
    public sealed class VoiceCommandKeywordConfig
    {
        public VoiceCommand Command { get; set; }

        public string WakeWord { get; set; } = string.Empty;

        public float KeywordThreshold { get; set; }

        public VoiceCommandKeywordConfig Clone()
        {
            return new VoiceCommandKeywordConfig
            {
                Command = Command,
                WakeWord = WakeWord,
                KeywordThreshold = KeywordThreshold
            };
        }
    }
}
