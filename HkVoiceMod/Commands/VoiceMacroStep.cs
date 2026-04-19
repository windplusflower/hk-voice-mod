using System;
using System.Collections.Generic;

namespace HkVoiceMod.Commands
{
    [Serializable]
    public sealed class VoiceMacroStep
    {
        public VoiceMacroStepKind StepKind { get; set; }

        public List<global::GlobalEnums.HeroActionButton> ActionButtons { get; set; } = new List<global::GlobalEnums.HeroActionButton>();

        public List<HeroActionKey> Keys { get; set; } = new List<HeroActionKey>();

        public KeyPressMode PressMode { get; set; }

        public float DurationSeconds { get; set; }

        public bool ReleaseOppositeHorizontalHold { get; set; }

        public float DelaySeconds { get; set; }

        public VoiceMacroStep Clone()
        {
            return new VoiceMacroStep
            {
                StepKind = StepKind,
                ActionButtons = new List<global::GlobalEnums.HeroActionButton>(ActionButtons ?? new List<global::GlobalEnums.HeroActionButton>()),
                Keys = new List<HeroActionKey>(Keys ?? new List<HeroActionKey>()),
                PressMode = PressMode,
                DurationSeconds = DurationSeconds,
                ReleaseOppositeHorizontalHold = ReleaseOppositeHorizontalHold,
                DelaySeconds = DelaySeconds
            };
        }

        public List<global::GlobalEnums.HeroActionButton> GetNormalizedActionButtons()
        {
            if (ActionButtons != null && ActionButtons.Count > 0)
            {
                return new List<global::GlobalEnums.HeroActionButton>(ActionButtons);
            }

            var migrated = HeroActionButtonCatalog.MapLegacyKeys(Keys);
            ActionButtons = new List<global::GlobalEnums.HeroActionButton>(migrated);
            return migrated;
        }

        public static VoiceMacroStep CreateDelay(float delaySeconds)
        {
            return new VoiceMacroStep
            {
                StepKind = VoiceMacroStepKind.Delay,
                DelaySeconds = delaySeconds
            };
        }

        public static VoiceMacroStep CreateAction(IReadOnlyList<global::GlobalEnums.HeroActionButton> actionButtons, KeyPressMode pressMode, float durationSeconds, bool releaseOppositeHorizontalHold)
        {
            return new VoiceMacroStep
            {
                StepKind = VoiceMacroStepKind.Action,
                ActionButtons = actionButtons == null ? new List<global::GlobalEnums.HeroActionButton>() : new List<global::GlobalEnums.HeroActionButton>(actionButtons),
                Keys = new List<HeroActionKey>(),
                PressMode = pressMode,
                DurationSeconds = durationSeconds,
                ReleaseOppositeHorizontalHold = releaseOppositeHorizontalHold
            };
        }
    }
}
