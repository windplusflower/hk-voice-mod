using System;
using System.Collections.Generic;

namespace HkVoiceMod.Commands
{
    public sealed class KeyActionProfile
    {
        public KeyActionProfile(
            VoiceCommand command,
            KeyPressMode mode,
            IReadOnlyList<HeroActionKey> keys,
            float durationSeconds,
            bool releaseOppositeHorizontalHold)
        {
            Command = command;
            Mode = mode;
            Keys = keys ?? throw new ArgumentNullException(nameof(keys));
            DurationSeconds = durationSeconds;
            ReleaseOppositeHorizontalHold = releaseOppositeHorizontalHold;
        }

        public VoiceCommand Command { get; }

        public KeyPressMode Mode { get; }

        public IReadOnlyList<HeroActionKey> Keys { get; }

        public float DurationSeconds { get; }

        public bool ReleaseOppositeHorizontalHold { get; }
    }
}
