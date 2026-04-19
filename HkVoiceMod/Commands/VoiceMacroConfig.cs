using System;
using System.Collections.Generic;

namespace HkVoiceMod.Commands
{
    [Serializable]
    public sealed class VoiceMacroConfig
    {
        public string Id { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string WakeWord { get; set; } = string.Empty;

        public float KeywordThreshold { get; set; }

        public List<VoiceMacroKeyEvent> KeyEvents { get; set; } = new List<VoiceMacroKeyEvent>();

        public List<VoiceMacroStep> Steps { get; set; } = new List<VoiceMacroStep>();

        public bool IsPreset { get; set; }

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
                KeyEvents = clonedKeyEvents,
                Steps = clonedSteps,
                IsPreset = IsPreset
            };
        }
    }
}
