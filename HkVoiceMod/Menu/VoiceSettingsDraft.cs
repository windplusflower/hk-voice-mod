using System;
using System.Collections.Generic;
using HkVoiceMod.Commands;

namespace HkVoiceMod.Menu
{
    internal sealed class VoiceSettingsDraft
    {
        private readonly Dictionary<string, float> _pendingDelaySecondsByMacroId = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly VoiceModSettings _pendingSettings;

        private VoiceSettingsDraft(VoiceModSettings pendingSettings)
        {
            _pendingSettings = pendingSettings;
        }

        public StopKeywordConfig PendingStopKeywordConfig => _pendingSettings.StopKeywordConfig;

        public List<VoiceMacroConfig> PendingMacroConfigs => _pendingSettings.MacroConfigs;

        public static VoiceSettingsDraft FromAppliedSettings(VoiceModSettings settings)
        {
            var clone = settings?.Clone() ?? new VoiceModSettings();
            clone.MigrateLegacyCommandConfigsIfNeeded();
            clone.EnsureMacroDefaults();
            return new VoiceSettingsDraft(clone);
        }

        public VoiceModSettings CreateSettingsSnapshot()
        {
            return _pendingSettings.Clone();
        }

        public void AddMacro(VoiceMacroConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            PendingMacroConfigs.Add(config);
            if (!_pendingDelaySecondsByMacroId.ContainsKey(config.Id))
            {
                _pendingDelaySecondsByMacroId[config.Id] = 0.5f;
            }
        }

        public void RemoveMacro(string macroId)
        {
            for (var index = PendingMacroConfigs.Count - 1; index >= 0; index--)
            {
                if (string.Equals(PendingMacroConfigs[index].Id, macroId, StringComparison.Ordinal))
                {
                    PendingMacroConfigs.RemoveAt(index);
                }
            }

            _pendingDelaySecondsByMacroId.Remove(macroId);
        }

        public VoiceMacroConfig? FindMacro(string macroId)
        {
            foreach (var macro in PendingMacroConfigs)
            {
                if (string.Equals(macro.Id, macroId, StringComparison.Ordinal))
                {
                    return macro;
                }
            }

            return null;
        }

        public float GetPendingDelaySeconds(string macroId)
        {
            return _pendingDelaySecondsByMacroId.TryGetValue(macroId, out var delaySeconds) ? delaySeconds : 0.5f;
        }

        public void SetPendingDelaySeconds(string macroId, float delaySeconds)
        {
            _pendingDelaySecondsByMacroId[macroId] = delaySeconds;
        }

        public bool HasPendingChanges(VoiceModSettings appliedSettings)
        {
            var pending = _pendingSettings.Clone();
            var applied = appliedSettings?.Clone() ?? new VoiceModSettings();

            pending.MigrateLegacyCommandConfigsIfNeeded();
            pending.EnsureMacroDefaults();
            applied.MigrateLegacyCommandConfigsIfNeeded();
            applied.EnsureMacroDefaults();

            var pendingStop = pending.StopKeywordConfig;
            var appliedStop = applied.StopKeywordConfig;
            if (!string.Equals(VoiceModSettings.NormalizeWakeWord(pendingStop.WakeWord), VoiceModSettings.NormalizeWakeWord(appliedStop.WakeWord), StringComparison.Ordinal))
            {
                return true;
            }

            if (Math.Abs(pendingStop.KeywordThreshold - appliedStop.KeywordThreshold) > 0.0001f)
            {
                return true;
            }

            var pendingMacros = pending.GetOrderedMacroConfigs();
            var appliedMacros = applied.GetOrderedMacroConfigs();
            if (pendingMacros.Count != appliedMacros.Count)
            {
                return true;
            }

            for (var index = 0; index < pendingMacros.Count; index++)
            {
                if (MacroChanged(pendingMacros[index], appliedMacros[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MacroChanged(VoiceMacroConfig pending, VoiceMacroConfig applied)
        {
            if (!string.Equals(pending.Id, applied.Id, StringComparison.Ordinal)
                || !string.Equals(pending.DisplayName, applied.DisplayName, StringComparison.Ordinal)
                || !string.Equals(VoiceModSettings.NormalizeWakeWord(pending.WakeWord), VoiceModSettings.NormalizeWakeWord(applied.WakeWord), StringComparison.Ordinal)
                || Math.Abs(pending.KeywordThreshold - applied.KeywordThreshold) > 0.0001f
                || pending.IsPreset != applied.IsPreset)
            {
                return true;
            }

            if ((pending.Steps?.Count ?? 0) != (applied.Steps?.Count ?? 0))
            {
                return true;
            }

            if (pending.Steps == null || applied.Steps == null)
            {
                return false;
            }

            for (var index = 0; index < pending.Steps.Count; index++)
            {
                if (StepChanged(pending.Steps[index], applied.Steps[index]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool StepChanged(VoiceMacroStep pending, VoiceMacroStep applied)
        {
            if (pending.StepKind != applied.StepKind
                || pending.PressMode != applied.PressMode
                || Math.Abs(pending.DurationSeconds - applied.DurationSeconds) > 0.0001f
                || pending.ReleaseOppositeHorizontalHold != applied.ReleaseOppositeHorizontalHold
                || Math.Abs(pending.DelaySeconds - applied.DelaySeconds) > 0.0001f)
            {
                return true;
            }

            if ((pending.Keys?.Count ?? 0) != (applied.Keys?.Count ?? 0))
            {
                return true;
            }

            if (pending.Keys == null || applied.Keys == null)
            {
                return false;
            }

            for (var index = 0; index < pending.Keys.Count; index++)
            {
                if (pending.Keys[index] != applied.Keys[index])
                {
                    return true;
                }
            }

            return false;
        }
    }
}
