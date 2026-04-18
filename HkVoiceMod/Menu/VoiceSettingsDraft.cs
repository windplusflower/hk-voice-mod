using HkVoiceMod.Commands;

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
    }
}
