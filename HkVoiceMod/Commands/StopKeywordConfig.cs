using System;
using System.Collections.Generic;

namespace HkVoiceMod.Commands
{
    [Serializable]
    public sealed class StopKeywordConfig : IVoiceTemplateOwner
    {
        private const string StopOwnerId = "stop";

        public string WakeWord { get; set; } = string.Empty;

        public float KeywordThreshold { get; set; }

        public bool EnableTemplateVerification { get; set; } = true;

        public List<VoiceTemplateConfig> Templates { get; set; } = new List<VoiceTemplateConfig>();

        public string TemplateOwnerId => StopOwnerId;

        public string TemplateDisplayName => "停止词";

        public StopKeywordConfig Clone()
        {
            return new StopKeywordConfig
            {
                WakeWord = WakeWord,
                KeywordThreshold = KeywordThreshold,
                EnableTemplateVerification = EnableTemplateVerification,
                Templates = CloneTemplates(Templates)
            };
        }

        public static StopKeywordConfig CreateDefault()
        {
            return new StopKeywordConfig
            {
                WakeWord = "停止",
                KeywordThreshold = 0.1f,
                EnableTemplateVerification = true,
                Templates = new List<VoiceTemplateConfig>()
            };
        }

        private static List<VoiceTemplateConfig> CloneTemplates(List<VoiceTemplateConfig> templates)
        {
            var clones = new List<VoiceTemplateConfig>(templates?.Count ?? 0);
            if (templates == null)
            {
                return clones;
            }

            foreach (var template in templates)
            {
                if (template != null)
                {
                    clones.Add(template.Clone());
                }
            }

            return clones;
        }
    }
}
