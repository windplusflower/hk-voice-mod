using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HkVoiceMod.Commands;

namespace HkVoiceMod
{
    [Serializable]
    public sealed class VoiceModSettings
    {
        public bool Enabled { get; set; } = true;

        public string SherpaModelPath { get; set; } = "assets/sherpa-kws-cn";

        public float ShortPressDurationSeconds { get; set; } = 0.08f;

        public float TimedHoldDurationSeconds { get; set; } = 0.5f;

        public int DuplicateCommandCooldownMilliseconds { get; set; } = 300;

        public int SampleRateHz { get; set; } = 16000;

        public int CaptureBufferMilliseconds { get; set; } = 50;

        public bool EnableVerboseLogging { get; set; } = true;

        public bool LogRecognizedText { get; set; } = true;

        public StopKeywordConfig StopKeywordConfig { get; set; } = global::HkVoiceMod.Commands.StopKeywordConfig.CreateDefault();

        public List<VoiceMacroConfig> MacroConfigs { get; set; } = new List<VoiceMacroConfig>();

        public List<VoiceCommandKeywordConfig> CommandKeywordConfigs { get; set; } = VoiceCommandCatalog.CreateDefaultKeywordConfigs();

        public VoiceModSettings Clone()
        {
            return new VoiceModSettings
            {
                Enabled = Enabled,
                SherpaModelPath = SherpaModelPath,
                ShortPressDurationSeconds = ShortPressDurationSeconds,
                TimedHoldDurationSeconds = TimedHoldDurationSeconds,
                DuplicateCommandCooldownMilliseconds = DuplicateCommandCooldownMilliseconds,
                SampleRateHz = SampleRateHz,
                CaptureBufferMilliseconds = CaptureBufferMilliseconds,
                EnableVerboseLogging = EnableVerboseLogging,
                LogRecognizedText = LogRecognizedText,
                StopKeywordConfig = StopKeywordConfig?.Clone() ?? global::HkVoiceMod.Commands.StopKeywordConfig.CreateDefault(),
                MacroConfigs = CloneMacroConfigs(MacroConfigs),
                CommandKeywordConfigs = CloneCommandKeywordConfigs(CommandKeywordConfigs)
            };
        }

        public void EnsureMacroDefaults()
        {
            StopKeywordConfig = StopKeywordConfig?.Clone() ?? global::HkVoiceMod.Commands.StopKeywordConfig.CreateDefault();
            MacroConfigs = CloneMacroConfigs(MacroConfigs);
        }

        public void EnsureCommandKeywordDefaults()
        {
            var existingByCommand = new Dictionary<VoiceCommand, VoiceCommandKeywordConfig>();
            if (CommandKeywordConfigs != null)
            {
                foreach (var config in CommandKeywordConfigs)
                {
                    if (config == null || existingByCommand.ContainsKey(config.Command))
                    {
                        continue;
                    }

                    existingByCommand[config.Command] = config.Clone();
                }
            }

            CommandKeywordConfigs = new List<VoiceCommandKeywordConfig>(VoiceCommandCatalog.All.Count);
            foreach (var definition in VoiceCommandCatalog.All)
            {
                if (existingByCommand.TryGetValue(definition.Command, out var existing))
                {
                    if (string.IsNullOrWhiteSpace(existing.WakeWord))
                    {
                        existing.WakeWord = definition.DefaultWakeWord;
                    }

                    if (!IsThresholdInRange(existing.KeywordThreshold))
                    {
                        existing.KeywordThreshold = definition.DefaultThreshold;
                    }

                    CommandKeywordConfigs.Add(existing);
                    continue;
                }

                CommandKeywordConfigs.Add(new VoiceCommandKeywordConfig
                {
                    Command = definition.Command,
                    WakeWord = definition.DefaultWakeWord,
                    KeywordThreshold = definition.DefaultThreshold
                });
            }
        }

        public IReadOnlyList<VoiceCommandKeywordConfig> GetOrderedCommandKeywordConfigs()
        {
            EnsureCommandKeywordDefaults();
            return CommandKeywordConfigs;
        }

        public IReadOnlyList<VoiceMacroConfig> GetOrderedMacroConfigs()
        {
            EnsureMacroDefaults();
            return MacroConfigs;
        }

        public void MigrateLegacyCommandConfigsIfNeeded()
        {
            EnsureMacroDefaults();
            if (MacroConfigs.Count > 0)
            {
                return;
            }

            EnsureCommandKeywordDefaults();
            var migratedMacros = new List<VoiceMacroConfig>();
            var stopConfig = global::HkVoiceMod.Commands.StopKeywordConfig.CreateDefault();

            foreach (var legacyConfig in GetOrderedCommandKeywordConfigs())
            {
                if (legacyConfig.Command == VoiceCommand.Stop)
                {
                    stopConfig = new global::HkVoiceMod.Commands.StopKeywordConfig
                    {
                        WakeWord = legacyConfig.WakeWord,
                        KeywordThreshold = legacyConfig.KeywordThreshold
                    };
                    continue;
                }

                migratedMacros.Add(CreatePresetMacroFromLegacyCommand(legacyConfig));
            }

            StopKeywordConfig = stopConfig;
            MacroConfigs = migratedMacros;
        }

        public void NormalizeAndValidateMacroSettings()
        {
            EnsureMacroDefaults();

            var normalizedWakeWords = new HashSet<string>(StringComparer.Ordinal);
            var normalizedStopWakeWord = NormalizeWakeWord(StopKeywordConfig.WakeWord);
            if (normalizedStopWakeWord.Length == 0)
            {
                throw new InvalidOperationException("停止命令的唤醒词不能为空。");
            }

            if (!ContainsOnlySupportedWakeWordChars(normalizedStopWakeWord))
            {
                throw new InvalidOperationException($"停止命令的唤醒词仅支持中文字符：{normalizedStopWakeWord}");
            }

            if (!IsThresholdInRange(StopKeywordConfig.KeywordThreshold))
            {
                throw new InvalidOperationException("停止命令的阈值必须在 0.01 到 1.00 之间。");
            }

            normalizedWakeWords.Add(normalizedStopWakeWord);
            StopKeywordConfig.WakeWord = normalizedStopWakeWord;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < MacroConfigs.Count; index++)
            {
                var config = MacroConfigs[index];
                if (config == null)
                {
                    throw new InvalidOperationException($"第 {index + 1} 个宏配置为空。");
                }

                config.Id = (config.Id ?? string.Empty).Trim();
                if (config.Id.Length == 0)
                {
                    throw new InvalidOperationException($"第 {index + 1} 个宏缺少唯一 Id。");
                }

                if (!ids.Add(config.Id))
                {
                    throw new InvalidOperationException($"宏 Id 重复：{config.Id}");
                }

                config.DisplayName = (config.DisplayName ?? string.Empty).Trim();
                if (config.DisplayName.Length == 0)
                {
                    throw new InvalidOperationException($"宏 {config.Id} 的显示名不能为空。");
                }

                var normalizedWakeWord = NormalizeWakeWord(config.WakeWord);
                if (normalizedWakeWord.Length == 0)
                {
                    throw new InvalidOperationException($"宏 {config.DisplayName} 的唤醒词不能为空。");
                }

                if (!ContainsOnlySupportedWakeWordChars(normalizedWakeWord))
                {
                    throw new InvalidOperationException($"宏 {config.DisplayName} 的唤醒词仅支持中文字符：{normalizedWakeWord}");
                }

                if (!normalizedWakeWords.Add(normalizedWakeWord))
                {
                    throw new InvalidOperationException($"唤醒词重复：{normalizedWakeWord}");
                }

                if (!IsThresholdInRange(config.KeywordThreshold))
                {
                    throw new InvalidOperationException($"宏 {config.DisplayName} 的阈值必须在 0.01 到 1.00 之间。");
                }

                if (config.Steps == null || config.Steps.Count == 0)
                {
                    throw new InvalidOperationException($"宏 {config.DisplayName} 至少需要一个步骤。");
                }

                for (var stepIndex = 0; stepIndex < config.Steps.Count; stepIndex++)
                {
                    ValidateMacroStep(config, config.Steps[stepIndex], stepIndex);
                }

                config.WakeWord = normalizedWakeWord;
            }
        }

        public void NormalizeAndValidateCommandKeywordConfigs()
        {
            EnsureCommandKeywordDefaults();

            var normalizedWakeWords = new HashSet<string>(StringComparer.Ordinal);
            foreach (var config in CommandKeywordConfigs)
            {
                var normalizedWakeWord = NormalizeWakeWord(config.WakeWord);
                if (normalizedWakeWord.Length == 0)
                {
                    throw new InvalidOperationException($"命令 {config.Command} 的唤醒词不能为空。");
                }

                if (!ContainsOnlySupportedWakeWordChars(normalizedWakeWord))
                {
                    throw new InvalidOperationException($"命令 {config.Command} 的唤醒词仅支持中文字符：{normalizedWakeWord}");
                }

                if (!normalizedWakeWords.Add(normalizedWakeWord))
                {
                    throw new InvalidOperationException($"唤醒词重复：{normalizedWakeWord}");
                }

                if (!IsThresholdInRange(config.KeywordThreshold))
                {
                    throw new InvalidOperationException($"命令 {config.Command} 的阈值必须在 0.01 到 1.00 之间。");
                }

                config.WakeWord = normalizedWakeWord;
            }
        }

        public static string NormalizeWakeWord(string wakeWord)
        {
            if (wakeWord == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder(wakeWord.Length);
            foreach (var character in wakeWord.Trim())
            {
                if (character == '@')
                {
                    continue;
                }

                if (char.IsWhiteSpace(character))
                {
                    continue;
                }

                builder.Append(character);
            }

            return builder.ToString();
        }

        public string ResolveModelPath(string assemblyDirectory)
        {
            if (Path.IsPathRooted(SherpaModelPath))
            {
                return SherpaModelPath;
            }

            return Path.GetFullPath(Path.Combine(assemblyDirectory, SherpaModelPath));
        }

        private static bool ContainsOnlySupportedWakeWordChars(string wakeWord)
        {
            foreach (var character in wakeWord)
            {
                if (character < 0x4E00 || character > 0x9FFF)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsThresholdInRange(float threshold)
        {
            return !float.IsNaN(threshold) && !float.IsInfinity(threshold) && threshold >= 0.01f && threshold <= 1.0f;
        }

        private VoiceMacroConfig CreatePresetMacroFromLegacyCommand(VoiceCommandKeywordConfig legacyConfig)
        {
            var definition = VoiceCommandCatalog.GetDefinition(legacyConfig.Command);
            var displayName = legacyConfig.Command == VoiceCommand.Cast ? "法术" : definition.DisplayName;
            var wakeWord = legacyConfig.WakeWord;
            if (legacyConfig.Command == VoiceCommand.Cast && string.Equals(NormalizeWakeWord(wakeWord), "放波", StringComparison.Ordinal))
            {
                wakeWord = "法术";
            }

            var profile = VoiceCommandMap.GetProfile(legacyConfig.Command, this);
            return new VoiceMacroConfig
            {
                Id = $"legacy-{legacyConfig.Command.ToString().ToLowerInvariant()}",
                DisplayName = displayName,
                WakeWord = wakeWord,
                KeywordThreshold = legacyConfig.KeywordThreshold,
                Steps = profile.Mode == KeyPressMode.ReleaseContinuous
                    ? new List<VoiceMacroStep>()
                    : new List<VoiceMacroStep>
                    {
                        VoiceMacroStep.CreateAction(HeroActionButtonCatalog.MapLegacyKeys(profile.Keys), profile.Mode, profile.DurationSeconds, profile.ReleaseOppositeHorizontalHold)
                    },
                IsPreset = true
            };
        }

        private static void ValidateMacroStep(VoiceMacroConfig config, VoiceMacroStep step, int stepIndex)
        {
            if (step == null)
            {
                throw new InvalidOperationException($"宏 {config.DisplayName} 的第 {stepIndex + 1} 个步骤为空。");
            }

            if (step.StepKind == VoiceMacroStepKind.Delay)
            {
                if (step.DelaySeconds <= 0f || float.IsNaN(step.DelaySeconds) || float.IsInfinity(step.DelaySeconds))
                {
                    throw new InvalidOperationException($"宏 {config.DisplayName} 的第 {stepIndex + 1} 个延迟步骤无效。");
                }

                return;
            }

            if (step.StepKind != VoiceMacroStepKind.Action)
            {
                throw new InvalidOperationException($"宏 {config.DisplayName} 的第 {stepIndex + 1} 个步骤类型不受支持。");
            }

            var actionButtons = step.GetNormalizedActionButtons();
            if (actionButtons.Count == 0)
            {
                throw new InvalidOperationException($"宏 {config.DisplayName} 的第 {stepIndex + 1} 个动作步骤缺少按键。");
            }

            foreach (var actionButton in actionButtons)
            {
                if (!Enum.IsDefined(typeof(global::GlobalEnums.HeroActionButton), actionButton) || !ContainsSupportedActionButton(actionButton))
                {
                    throw new InvalidOperationException($"宏 {config.DisplayName} 的第 {stepIndex + 1} 个动作步骤包含无效按键：{actionButton}");
                }
            }

            if (step.PressMode == KeyPressMode.ReleaseContinuous)
            {
                throw new InvalidOperationException($"宏 {config.DisplayName} 的第 {stepIndex + 1} 个动作步骤不允许使用 ReleaseContinuous。");
            }

            if ((step.PressMode == KeyPressMode.TimedHold || step.PressMode == KeyPressMode.Tap) && (step.DurationSeconds <= 0f || float.IsNaN(step.DurationSeconds) || float.IsInfinity(step.DurationSeconds)))
            {
                throw new InvalidOperationException($"宏 {config.DisplayName} 的第 {stepIndex + 1} 个动作步骤持续时间无效。");
            }
        }

        private static bool ContainsSupportedActionButton(global::GlobalEnums.HeroActionButton actionButton)
        {
            var supportedButtons = HeroActionButtonCatalog.SupportedGameplayButtons;
            for (var index = 0; index < supportedButtons.Count; index++)
            {
                if (supportedButtons[index] == actionButton)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<VoiceCommandKeywordConfig> CloneCommandKeywordConfigs(List<VoiceCommandKeywordConfig> configs)
        {
            if (configs == null)
            {
                return VoiceCommandCatalog.CreateDefaultKeywordConfigs();
            }

            var clones = new List<VoiceCommandKeywordConfig>(configs.Count);
            foreach (var config in configs)
            {
                if (config != null)
                {
                    clones.Add(config.Clone());
                }
            }

            return clones;
        }

        private static List<VoiceMacroConfig> CloneMacroConfigs(List<VoiceMacroConfig> configs)
        {
            if (configs == null)
            {
                return new List<VoiceMacroConfig>();
            }

            var clones = new List<VoiceMacroConfig>(configs.Count);
            foreach (var config in configs)
            {
                if (config != null)
                {
                    clones.Add(config.Clone());
                }
            }

            return clones;
        }
    }
}
