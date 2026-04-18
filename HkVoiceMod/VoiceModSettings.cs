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
                CommandKeywordConfigs = CloneCommandKeywordConfigs(CommandKeywordConfigs)
            };
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
    }
}
