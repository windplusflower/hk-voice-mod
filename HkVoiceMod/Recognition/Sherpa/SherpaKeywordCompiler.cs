using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HkVoiceMod.Commands;

namespace HkVoiceMod.Recognition.Sherpa
{
    public sealed class SherpaKeywordCompiler
    {
        private readonly IPinyinProvider _pinyinProvider;

        public SherpaKeywordCompiler(IPinyinProvider pinyinProvider)
        {
            _pinyinProvider = pinyinProvider ?? throw new ArgumentNullException(nameof(pinyinProvider));
        }

        public SherpaKeywordArtifacts Compile(string modelDirectory, StopKeywordConfig stopConfig, IReadOnlyList<VoiceMacroConfig> macroConfigs)
        {
            if (string.IsNullOrWhiteSpace(modelDirectory))
            {
                throw new ArgumentException("Model directory is required.", nameof(modelDirectory));
            }

            if (stopConfig == null)
            {
                throw new ArgumentNullException(nameof(stopConfig));
            }

            if (macroConfigs == null)
            {
                throw new ArgumentNullException(nameof(macroConfigs));
            }

            var generatedDirectory = Path.Combine(modelDirectory, SherpaKeywordArtifacts.GeneratedDirectoryName);
            Directory.CreateDirectory(generatedDirectory);

            var rawKeywordsPath = Path.Combine(generatedDirectory, SherpaKeywordArtifacts.RawKeywordsFileName);
            var compiledKeywordsPath = Path.Combine(generatedDirectory, SherpaKeywordArtifacts.KeywordsFileName);

            var rawLines = new List<string>(macroConfigs.Count + 1);
            var compiledLines = new List<string>(macroConfigs.Count + 1);
            var lookup = new Dictionary<string, VoiceTriggerRef>(macroConfigs.Count + 1, StringComparer.Ordinal);

            AppendEntry(rawLines, compiledLines, lookup, stopConfig.WakeWord, stopConfig.KeywordThreshold, new VoiceTriggerRef(VoiceTriggerKind.Stop, "stop"));

            foreach (var config in macroConfigs)
            {
                AppendEntry(rawLines, compiledLines, lookup, config.WakeWord, config.KeywordThreshold, new VoiceTriggerRef(VoiceTriggerKind.Macro, config.Id));
            }

            WriteAllLinesReplacing(rawKeywordsPath, rawLines);
            WriteAllLinesReplacing(compiledKeywordsPath, compiledLines);
            return new SherpaKeywordArtifacts(rawKeywordsPath, compiledKeywordsPath, lookup);
        }

        private void AppendEntry(
            ICollection<string> rawLines,
            ICollection<string> compiledLines,
            IDictionary<string, VoiceTriggerRef> lookup,
            string wakeWordText,
            float keywordThreshold,
            VoiceTriggerRef triggerRef)
        {
            var wakeWord = VoiceModSettings.NormalizeWakeWord(wakeWordText);
            var thresholdText = keywordThreshold.ToString("0.####", CultureInfo.InvariantCulture);
            rawLines.Add($"{wakeWord} #{thresholdText} @{wakeWord}");

            var tokens = new List<string>();
            foreach (var syllable in _pinyinProvider.ConvertToSyllables(wakeWord))
            {
                if (syllable.Initial.Length > 0)
                {
                    tokens.Add(syllable.Initial);
                }

                if (syllable.FinalWithTone.Length > 0)
                {
                    tokens.Add(syllable.FinalWithTone);
                }
            }

            if (tokens.Count == 0)
            {
                throw new InvalidOperationException($"唤醒词未能生成任何 Sherpa token：{wakeWord}");
            }

            compiledLines.Add($"{string.Join(" ", tokens)} #{thresholdText} @{wakeWord}");
            lookup.Add(wakeWord, triggerRef);
        }

        private static void WriteAllLinesReplacing(string destinationPath, IReadOnlyList<string> lines)
        {
            var directory = Path.GetDirectoryName(destinationPath);
            if (directory == null)
            {
                throw new InvalidOperationException($"无效的目标路径：{destinationPath}");
            }

            Directory.CreateDirectory(directory);
            var tempPath = destinationPath + ".tmp";
            File.WriteAllLines(tempPath, lines);

            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            File.Move(tempPath, destinationPath);
        }
    }
}
