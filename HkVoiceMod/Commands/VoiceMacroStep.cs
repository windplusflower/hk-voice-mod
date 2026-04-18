using System;
using System.Collections.Generic;

namespace HkVoiceMod.Commands
{
    [Serializable]
    public sealed class VoiceMacroStep
    {
        public VoiceMacroStepKind StepKind { get; set; }

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
                Keys = new List<HeroActionKey>(Keys ?? new List<HeroActionKey>()),
                PressMode = PressMode,
                DurationSeconds = DurationSeconds,
                ReleaseOppositeHorizontalHold = ReleaseOppositeHorizontalHold,
                DelaySeconds = DelaySeconds
            };
        }

        public static VoiceMacroStep CreateDelay(float delaySeconds)
        {
            return new VoiceMacroStep
            {
                StepKind = VoiceMacroStepKind.Delay,
                DelaySeconds = delaySeconds
            };
        }

        public static VoiceMacroStep CreateAction(IReadOnlyList<HeroActionKey> keys, KeyPressMode pressMode, float durationSeconds, bool releaseOppositeHorizontalHold)
        {
            return new VoiceMacroStep
            {
                StepKind = VoiceMacroStepKind.Action,
                Keys = keys == null ? new List<HeroActionKey>() : new List<HeroActionKey>(keys),
                PressMode = pressMode,
                DurationSeconds = durationSeconds,
                ReleaseOppositeHorizontalHold = releaseOppositeHorizontalHold
            };
        }
    }
}
