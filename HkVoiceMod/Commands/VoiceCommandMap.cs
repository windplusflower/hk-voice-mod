using System;

namespace HkVoiceMod.Commands
{
    public static class VoiceCommandMap
    {
        public static KeyActionProfile GetProfile(VoiceCommand command, VoiceModSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            switch (command)
            {
                case VoiceCommand.Up:
                    return Timed(command, settings.TimedHoldDurationSeconds, HeroActionKey.Up);
                case VoiceCommand.Down:
                    return Timed(command, settings.TimedHoldDurationSeconds, HeroActionKey.Down);
                case VoiceCommand.Left:
                    return new KeyActionProfile(command, KeyPressMode.ContinuousHold, new[] { HeroActionKey.Left }, 0f, true);
                case VoiceCommand.Right:
                    return new KeyActionProfile(command, KeyPressMode.ContinuousHold, new[] { HeroActionKey.Right }, 0f, true);
                case VoiceCommand.Attack:
                    return Tap(command, settings.ShortPressDurationSeconds, HeroActionKey.Attack);
                case VoiceCommand.Jump:
                    return Timed(command, settings.TimedHoldDurationSeconds, HeroActionKey.Jump);
                case VoiceCommand.Dash:
                    return Tap(command, settings.ShortPressDurationSeconds, HeroActionKey.Dash);
                case VoiceCommand.Howl:
                    return Tap(command, settings.ShortPressDurationSeconds, HeroActionKey.Up, HeroActionKey.Cast);
                case VoiceCommand.Dive:
                    return Tap(command, settings.ShortPressDurationSeconds, HeroActionKey.Down, HeroActionKey.Cast);
                case VoiceCommand.Cast:
                    return Tap(command, settings.ShortPressDurationSeconds, HeroActionKey.Cast);
                case VoiceCommand.Stop:
                    return new KeyActionProfile(command, KeyPressMode.ReleaseContinuous, Array.Empty<HeroActionKey>(), 0f, false);
                default:
                    throw new ArgumentOutOfRangeException(nameof(command), command, "Unsupported voice command.");
            }
        }

        private static KeyActionProfile Tap(VoiceCommand command, float durationSeconds, params HeroActionKey[] keys)
        {
            return new KeyActionProfile(command, KeyPressMode.Tap, keys, durationSeconds, false);
        }

        private static KeyActionProfile Timed(VoiceCommand command, float durationSeconds, params HeroActionKey[] keys)
        {
            return new KeyActionProfile(command, KeyPressMode.TimedHold, keys, durationSeconds, false);
        }
    }
}
