using System;
using System.Collections.Generic;

namespace HkVoiceMod.Commands
{
    [Serializable]
    public sealed class VoiceMacroConfig : IVoiceTemplateOwner
    {
        public string Id { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string WakeWord { get; set; } = string.Empty;

        public float KeywordThreshold { get; set; }

        public bool EnableTemplateVerification { get; set; } = true;

        public List<VoiceTemplateConfig> Templates { get; set; } = new List<VoiceTemplateConfig>();

        public List<VoiceMacroKeyEvent> KeyEvents { get; set; } = new List<VoiceMacroKeyEvent>();

        public List<VoiceMacroStep> Steps { get; set; } = new List<VoiceMacroStep>();

        public bool IsPreset { get; set; }

        public string TemplateOwnerId => Id;

        public string TemplateDisplayName
        {
            get
            {
                var normalizedWakeWord = global::HkVoiceMod.VoiceModSettings.NormalizeWakeWord(WakeWord);
                if (normalizedWakeWord.Length > 0)
                {
                    return normalizedWakeWord;
                }

                return string.IsNullOrWhiteSpace(DisplayName) ? "未命名宏" : DisplayName;
            }
        }

        public VoiceMacroConfig Clone()
        {
            var clonedKeyEvents = new List<VoiceMacroKeyEvent>(KeyEvents?.Count ?? 0);
            if (KeyEvents != null)
            {
                foreach (var keyEvent in KeyEvents)
                {
                    if (keyEvent != null)
                    {
                        clonedKeyEvents.Add(keyEvent.Clone());
                    }
                }
            }

            var clonedSteps = new List<VoiceMacroStep>(Steps?.Count ?? 0);
            if (Steps != null)
            {
                foreach (var step in Steps)
                {
                    if (step != null)
                    {
                        clonedSteps.Add(step.Clone());
                    }
                }
            }

            return new VoiceMacroConfig
            {
                Id = Id,
                DisplayName = DisplayName,
                WakeWord = WakeWord,
                KeywordThreshold = KeywordThreshold,
                EnableTemplateVerification = EnableTemplateVerification,
                Templates = CloneTemplates(Templates),
                KeyEvents = clonedKeyEvents,
                Steps = clonedSteps,
                IsPreset = IsPreset
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
