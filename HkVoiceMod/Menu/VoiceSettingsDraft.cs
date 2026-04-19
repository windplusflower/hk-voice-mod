using System;
using System.Collections.Generic;
using System.Globalization;
using HkVoiceMod.Commands;

namespace HkVoiceMod.Menu
{
    internal sealed class VoiceSettingsDraft
    {
        private const float InvalidThresholdSentinel = -1f;

        private readonly Dictionary<string, float> _pendingDelaySecondsByMacroId = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _pendingThresholdTextByMacroId = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly VoiceModSettings _pendingSettings;

        private VoiceSettingsDraft(VoiceModSettings pendingSettings)
        {
            _pendingSettings = pendingSettings;
        }

        public StopKeywordConfig PendingStopKeywordConfig => _pendingSettings.StopKeywordConfig;

        public List<VoiceMacroConfig> PendingMacroConfigs => _pendingSettings.MacroConfigs;

        public string? SelectedMacroId { get; private set; }

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

            if (!_pendingThresholdTextByMacroId.ContainsKey(config.Id))
            {
                _pendingThresholdTextByMacroId[config.Id] = FormatThresholdText(config.KeywordThreshold);
            }

            SelectMacro(config.Id);
        }

        public void RemoveMacro(string macroId)
        {
            var removedIndex = -1;
            for (var index = PendingMacroConfigs.Count - 1; index >= 0; index--)
            {
                if (string.Equals(PendingMacroConfigs[index].Id, macroId, StringComparison.Ordinal))
                {
                    removedIndex = index;
                    PendingMacroConfigs.RemoveAt(index);
                }
            }

            _pendingDelaySecondsByMacroId.Remove(macroId);
            _pendingThresholdTextByMacroId.Remove(macroId);

            if (!string.Equals(SelectedMacroId, macroId, StringComparison.Ordinal))
            {
                return;
            }

            if (PendingMacroConfigs.Count == 0)
            {
                SelectedMacroId = null;
                return;
            }

            if (removedIndex < 0)
            {
                SelectedMacroId = null;
                return;
            }

            var nextIndex = Math.Min(removedIndex, PendingMacroConfigs.Count - 1);
            SelectedMacroId = PendingMacroConfigs[nextIndex].Id;
        }

        public void SelectMacro(string macroId)
        {
            if (string.IsNullOrWhiteSpace(macroId))
            {
                SelectedMacroId = null;
                return;
            }

            SelectedMacroId = macroId;
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

        public List<VoiceMacroStep> CloneMacroSteps(string macroId)
        {
            var macro = FindMacro(macroId);
            return CloneSteps(macro?.Steps);
        }

        public void ReplaceMacroSteps(string macroId, List<VoiceMacroStep> steps)
        {
            var macro = FindMacro(macroId);
            if (macro == null)
            {
                return;
            }

            macro.Steps = CloneSteps(steps);
        }

        public float GetPendingDelaySeconds(string macroId)
        {
            return _pendingDelaySecondsByMacroId.TryGetValue(macroId, out var delaySeconds) ? delaySeconds : 0.5f;
        }

        public void SetPendingDelaySeconds(string macroId, float delaySeconds)
        {
            _pendingDelaySecondsByMacroId[macroId] = delaySeconds;
        }

        public int GetPendingDelayMilliseconds(string macroId)
        {
            return (int)Math.Round(GetPendingDelaySeconds(macroId) * 1000f, MidpointRounding.AwayFromZero);
        }

        public void SetPendingDelayMilliseconds(string macroId, int delayMilliseconds)
        {
            var normalizedDelayMilliseconds = Math.Max(0, delayMilliseconds);
            SetPendingDelaySeconds(macroId, normalizedDelayMilliseconds / 1000f);
        }

        public string GetPendingThresholdText(string macroId)
        {
            if (_pendingThresholdTextByMacroId.TryGetValue(macroId, out var thresholdText))
            {
                return thresholdText;
            }

            var macro = FindMacro(macroId);
            return macro == null ? string.Empty : FormatThresholdText(macro.KeywordThreshold);
        }

        public void SetPendingThresholdText(string macroId, string thresholdText)
        {
            var normalizedText = (thresholdText ?? string.Empty).Trim();
            _pendingThresholdTextByMacroId[macroId] = normalizedText;

            var macro = FindMacro(macroId);
            if (macro == null)
            {
                return;
            }

            if (normalizedText.Length == 0)
            {
                macro.KeywordThreshold = InvalidThresholdSentinel;
                return;
            }

            if (TryParseThresholdText(normalizedText, out var threshold))
            {
                macro.KeywordThreshold = threshold;
                return;
            }

            macro.KeywordThreshold = InvalidThresholdSentinel;
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
            var pendingDisplayName = GetComparableDisplayName(pending);
            var appliedDisplayName = GetComparableDisplayName(applied);

            if (!string.Equals(pending.Id, applied.Id, StringComparison.Ordinal)
                || !string.Equals(pendingDisplayName, appliedDisplayName, StringComparison.Ordinal)
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

        private static string GetComparableDisplayName(VoiceMacroConfig config)
        {
            var normalizedWakeWord = VoiceModSettings.NormalizeWakeWord(config.WakeWord);
            if (normalizedWakeWord.Length > 0)
            {
                return normalizedWakeWord;
            }

            return (config.DisplayName ?? string.Empty).Trim();
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

        private static List<VoiceMacroStep> CloneSteps(List<VoiceMacroStep>? steps)
        {
            var clones = new List<VoiceMacroStep>(steps?.Count ?? 0);
            if (steps == null)
            {
                return clones;
            }

            foreach (var step in steps)
            {
                if (step != null)
                {
                    clones.Add(step.Clone());
                }
            }

            return clones;
        }

        private static string FormatThresholdText(float threshold)
        {
            if (threshold >= 0.01f && threshold <= 1.0f && !float.IsNaN(threshold) && !float.IsInfinity(threshold))
            {
                return threshold.ToString("0.##", CultureInfo.InvariantCulture);
            }

            return string.Empty;
        }

        private static bool TryParseThresholdText(string thresholdText, out float threshold)
        {
            return float.TryParse(thresholdText, NumberStyles.Float, CultureInfo.InvariantCulture, out threshold)
                || float.TryParse(thresholdText, NumberStyles.Float, CultureInfo.CurrentCulture, out threshold);
        }
    }
}
