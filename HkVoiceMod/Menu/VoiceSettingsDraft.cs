using HkVoiceMod.Commands;
using System;

namespace HkVoiceMod.Menu
{
    internal sealed class VoiceSettingsDraft
    {
        private readonly VoiceModSettings _pendingSettings;

        private VoiceSettingsDraft(VoiceModSettings pendingSettings)
        {
            _pendingSettings = pendingSettings;
        }

        public System.Collections.Generic.List<VoiceCommandKeywordConfig> PendingCommandKeywordConfigs => _pendingSettings.CommandKeywordConfigs;

        public static VoiceSettingsDraft FromAppliedSettings(VoiceModSettings settings)
        {
            var clone = settings?.Clone() ?? new VoiceModSettings();
            clone.EnsureCommandKeywordDefaults();
            return new VoiceSettingsDraft(clone);
        }

        public VoiceModSettings CreateSettingsSnapshot()
        {
            return _pendingSettings.Clone();
        }

        public bool HasPendingChanges(VoiceModSettings appliedSettings)
        {
            var pending = _pendingSettings.Clone();
            var applied = appliedSettings?.Clone() ?? new VoiceModSettings();

            pending.EnsureCommandKeywordDefaults();
            applied.EnsureCommandKeywordDefaults();

            var pendingConfigs = pending.GetOrderedCommandKeywordConfigs();
            var appliedConfigs = applied.GetOrderedCommandKeywordConfigs();
            if (pendingConfigs.Count != appliedConfigs.Count)
            {
                return true;
            }

            for (var index = 0; index < pendingConfigs.Count; index++)
            {
                var pendingConfig = pendingConfigs[index];
                var appliedConfig = appliedConfigs[index];
                if (pendingConfig.Command != appliedConfig.Command)
                {
                    return true;
                }

                var pendingWakeWord = VoiceModSettings.NormalizeWakeWord(pendingConfig.WakeWord);
                var appliedWakeWord = VoiceModSettings.NormalizeWakeWord(appliedConfig.WakeWord);
                if (!string.Equals(pendingWakeWord, appliedWakeWord, StringComparison.Ordinal))
                {
                    return true;
                }

                if (Math.Abs(pendingConfig.KeywordThreshold - appliedConfig.KeywordThreshold) > 0.0001f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
