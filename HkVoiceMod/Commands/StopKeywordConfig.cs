using System;

namespace HkVoiceMod.Commands
{
    [Serializable]
    public sealed class StopKeywordConfig
    {
        public string WakeWord { get; set; } = string.Empty;

        public float KeywordThreshold { get; set; }

        public StopKeywordConfig Clone()
        {
            return new StopKeywordConfig
            {
                WakeWord = WakeWord,
                KeywordThreshold = KeywordThreshold
            };
        }

        public static StopKeywordConfig CreateDefault()
        {
            return new StopKeywordConfig
            {
                WakeWord = "停止",
                KeywordThreshold = 0.1f
            };
        }
    }
}
