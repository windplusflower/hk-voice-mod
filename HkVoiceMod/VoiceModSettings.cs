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
        private const int CurrentMacroStorageVersion = 2;

        public VoiceModSettings()
        {
            ResetToEventStreamDefaults();
        }

        public bool Enabled { get; set; } = true;

        public string SherpaModelPath { get; set; } = "assets/sherpa-kws-cn";

        public float ShortPressDurationSeconds { get; set; } = 0.08f;

        public float TimedHoldDurationSeconds { get; set; } = 0.5f;

        public int DuplicateCommandCooldownMilliseconds { get; set; } = 300;

        public int SampleRateHz { get; set; } = 16000;

        public int CaptureBufferMilliseconds { get; set; } = 50;

        public bool EnableVerboseLogging { get; set; } = true;

        public bool LogRecognizedText { get; set; } = true;

        public int MacroStorageVersion { get; set; } = CurrentMacroStorageVersion;

        public StopKeywordConfig StopKeywordConfig { get; set; } = global::HkVoiceMod.Commands.StopKeywordConfig.CreateDefault();

        public List<VoiceMacroConfig> MacroConfigs { get; set; } = new List<VoiceMacroConfig>();

        public List<VoiceCommandKeywordConfig> CommandKeywordConfigs { get; set; } = new List<VoiceCommandKeywordConfig>();

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
                MacroStorageVersion = MacroStorageVersion,
                StopKeywordConfig = StopKeywordConfig?.Clone() ?? global::HkVoiceMod.Commands.StopKeywordConfig.CreateDefault(),
                MacroConfigs = CloneMacroConfigs(MacroConfigs),
                CommandKeywordConfigs = CloneCommandKeywordConfigs(CommandKeywordConfigs)
            };
        }

        public void EnsureMacroDefaults()
        {
            StopKeywordConfig = StopKeywordConfig?.Clone() ?? global::HkVoiceMod.Commands.StopKeywordConfig.CreateDefault();
            MacroConfigs = CloneMacroConfigs(MacroConfigs);
            CommandKeywordConfigs = CloneCommandKeywordConfigs(CommandKeywordConfigs);
        }

        public IReadOnlyList<VoiceMacroConfig> GetOrderedMacroConfigs()
        {
            EnsureMacroDefaults();
            return MacroConfigs;
        }

        public IReadOnlyList<VoiceCommandKeywordConfig> GetOrderedCommandKeywordConfigs()
        {
            EnsureMacroDefaults();
            return CommandKeywordConfigs;
        }

        public bool RequiresResetToEventStreamDefaults()
        {
            if (MacroStorageVersion < CurrentMacroStorageVersion)
            {
                return true;
            }

            if (MacroConfigs == null)
            {
                return false;
            }

            foreach (var macro in MacroConfigs)
            {
                if (macro == null)
                {
                    return true;
                }

                if ((macro.Steps?.Count ?? 0) > 0)
                {
                    return true;
                }
            }

            return false;
        }

        public void ResetToEventStreamDefaults()
        {
            StopKeywordConfig = global::HkVoiceMod.Commands.StopKeywordConfig.CreateDefault();
            MacroConfigs = CreateDefaultEventStreamMacros();
            CommandKeywordConfigs = new List<VoiceCommandKeywordConfig>();
            MacroStorageVersion = CurrentMacroStorageVersion;
        }

        public void NormalizeAndValidateMacroSettings()
        {
            EnsureMacroDefaults();

            if (RequiresResetToEventStreamDefaults())
            {
                throw new InvalidOperationException("检测到旧版宏配置结构，必须先重置到新的事件流默认配置。");
            }

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

                ValidateMacroKeyEvents(config);
                config.WakeWord = normalizedWakeWord;
            }
        }

        public void NormalizeAndValidateCommandKeywordConfigs()
        {
            EnsureMacroDefaults();

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
                if (character == '@' || char.IsWhiteSpace(character))
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

        private List<VoiceMacroConfig> CreateDefaultEventStreamMacros()
        {
            var macros = new List<VoiceMacroConfig>();
            foreach (var definition in VoiceCommandCatalog.All)
            {
                if (definition.Command == VoiceCommand.Stop)
                {
                    continue;
                }

                var profile = VoiceCommandMap.GetProfile(definition.Command, this);
                var keyEvents = CreateDefaultEventSequence(definition.Command, profile);
                if (keyEvents.Count == 0)
                {
                    continue;
                }

                macros.Add(new VoiceMacroConfig
                {
                    Id = $"preset-{definition.Command.ToString().ToLowerInvariant()}",
                    DisplayName = definition.DisplayName,
                    WakeWord = definition.DefaultWakeWord,
                    KeywordThreshold = definition.DefaultThreshold,
                    KeyEvents = keyEvents,
                    Steps = new List<VoiceMacroStep>(),
                    IsPreset = true
                });
            }

            return macros;
        }

        private List<VoiceMacroKeyEvent> CreateDefaultEventSequence(VoiceCommand command, KeyActionProfile profile)
        {
            var actionButtons = HeroActionButtonCatalog.MapLegacyKeys(profile.Keys);
            var events = new List<VoiceMacroKeyEvent>(actionButtons.Count * 2);
            if (actionButtons.Count == 0)
            {
                return events;
            }

            for (var index = 0; index < actionButtons.Count; index++)
            {
                events.Add(new VoiceMacroKeyEvent
                {
                    DelayBeforeMilliseconds = 0,
                    ActionButton = actionButtons[index],
                    EventKind = VoiceMacroKeyEventKind.Down,
                    PairId = BuildDefaultPairId(command, index)
                });
            }

            if (profile.Mode == KeyPressMode.ContinuousHold)
            {
                return events;
            }

            var holdMilliseconds = ResolveDefaultHoldMilliseconds(profile);
            for (var index = 0; index < actionButtons.Count; index++)
            {
                events.Add(new VoiceMacroKeyEvent
                {
                    DelayBeforeMilliseconds = index == 0 ? holdMilliseconds : 0,
                    ActionButton = actionButtons[index],
                    EventKind = VoiceMacroKeyEventKind.Up,
                    PairId = BuildDefaultPairId(command, index)
                });
            }

            return events;
        }

        private int ResolveDefaultHoldMilliseconds(KeyActionProfile profile)
        {
            var durationSeconds = profile.Mode == KeyPressMode.ContinuousHold
                ? TimedHoldDurationSeconds
                : profile.DurationSeconds;

            var milliseconds = (int)Math.Round(Math.Max(durationSeconds, 0.001f) * 1000f, MidpointRounding.AwayFromZero);
            return Math.Max(1, milliseconds);
        }

        private static string BuildDefaultPairId(VoiceCommand command, int actionIndex)
        {
            return $"{command.ToString().ToLowerInvariant()}-{actionIndex}";
        }

        private static void ValidateMacroKeyEvents(VoiceMacroConfig config)
        {
            if (config.KeyEvents == null || config.KeyEvents.Count == 0)
            {
                throw new InvalidOperationException($"宏 {config.DisplayName} 至少需要一个事件。");
            }

            if ((config.Steps?.Count ?? 0) > 0)
            {
                throw new InvalidOperationException($"宏 {config.DisplayName} 仍包含旧版步骤数据，必须重置为事件流配置。");
            }

            if (config.KeyEvents[0] == null)
            {
                throw new InvalidOperationException($"宏 {config.DisplayName} 的第 1 个事件为空。");
            }

            if (config.KeyEvents[0].DelayBeforeMilliseconds != 0)
            {
                throw new InvalidOperationException($"宏 {config.DisplayName} 的第 1 个事件前置间隔必须为 0。");
            }

            var pairStates = new Dictionary<string, PairValidationState>(StringComparer.Ordinal);
            var activeActionButtons = new HashSet<global::GlobalEnums.HeroActionButton>();
            for (var index = 0; index < config.KeyEvents.Count; index++)
            {
                var keyEvent = config.KeyEvents[index];
                if (keyEvent == null)
                {
                    throw new InvalidOperationException($"宏 {config.DisplayName} 的第 {index + 1} 个事件为空。");
                }

                if (keyEvent.DelayBeforeMilliseconds < 0)
                {
                    throw new InvalidOperationException($"宏 {config.DisplayName} 的第 {index + 1} 个事件前置间隔无效。");
                }

                if (string.IsNullOrWhiteSpace(keyEvent.PairId))
                {
                    throw new InvalidOperationException($"宏 {config.DisplayName} 的第 {index + 1} 个事件缺少 PairId。");
                }

                if (!Enum.IsDefined(typeof(global::GlobalEnums.HeroActionButton), keyEvent.ActionButton) || !ContainsSupportedActionButton(keyEvent.ActionButton))
                {
                    throw new InvalidOperationException($"宏 {config.DisplayName} 的第 {index + 1} 个事件包含无效按键：{keyEvent.ActionButton}");
                }

                if (!pairStates.TryGetValue(keyEvent.PairId, out var pairState))
                {
                    pairState = new PairValidationState();
                }

                if (keyEvent.EventKind == VoiceMacroKeyEventKind.Down)
                {
                    if (pairState.HasDown)
                    {
                        throw new InvalidOperationException($"宏 {config.DisplayName} 的 PairId {keyEvent.PairId} 存在重复 Down。");
                    }

                    if (activeActionButtons.Contains(keyEvent.ActionButton))
                    {
                        throw new InvalidOperationException($"宏 {config.DisplayName} 中按键 {keyEvent.ActionButton} 存在重叠按住，当前事件流不支持同一动作键并发持有。");
                    }

                    pairState.HasDown = true;
                    pairState.ActionButton = keyEvent.ActionButton;
                    activeActionButtons.Add(keyEvent.ActionButton);
                }
                else
                {
                    if (!pairState.HasDown)
                    {
                        throw new InvalidOperationException($"宏 {config.DisplayName} 的 PairId {keyEvent.PairId} 在 Down 之前出现了 Up。");
                    }

                    if (pairState.HasUp)
                    {
                        throw new InvalidOperationException($"宏 {config.DisplayName} 的 PairId {keyEvent.PairId} 存在重复 Up。");
                    }

                    if (pairState.ActionButton != keyEvent.ActionButton)
                    {
                        throw new InvalidOperationException($"宏 {config.DisplayName} 的 PairId {keyEvent.PairId} 按键不一致。");
                    }

                    pairState.HasUp = true;
                    activeActionButtons.Remove(keyEvent.ActionButton);
                }

                pairStates[keyEvent.PairId] = pairState;
            }

            foreach (var pairState in pairStates)
            {
                if (!pairState.Value.HasDown)
                {
                    throw new InvalidOperationException($"宏 {config.DisplayName} 的 PairId {pairState.Key} 缺少 Down 事件。");
                }
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
                return new List<VoiceCommandKeywordConfig>();
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

        private struct PairValidationState
        {
            public bool HasDown;
            public bool HasUp;
            public global::GlobalEnums.HeroActionButton ActionButton;
        }
    }
}
