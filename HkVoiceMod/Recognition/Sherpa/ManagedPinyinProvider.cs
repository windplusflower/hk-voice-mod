using System;
using System.Collections.Generic;
using System.Text;
using hyjiacan.py4n;

namespace HkVoiceMod.Recognition.Sherpa
{
    public sealed class ManagedPinyinProvider : IPinyinProvider
    {
        private static readonly string[] Initials =
        {
            "zh", "ch", "sh",
            "b", "p", "m", "f", "d", "t", "n", "l", "g", "k", "h",
            "j", "q", "x", "r", "z", "c", "s", "y", "w"
        };

        private static readonly Dictionary<char, string> ToneMarks = new Dictionary<char, string>
        {
            ['a'] = "āáǎà",
            ['e'] = "ēéěè",
            ['i'] = "īíǐì",
            ['o'] = "ōóǒò",
            ['u'] = "ūúǔù",
            ['ü'] = "ǖǘǚǜ"
        };

        public IReadOnlyList<PinyinSyllableParts> ConvertToSyllables(string text)
        {
            var normalizedText = VoiceModSettings.NormalizeWakeWord(text);
            if (normalizedText.Length == 0)
            {
                throw new InvalidOperationException("唤醒词不能为空，无法生成拼音 token。");
            }

            var format = PinyinFormat.WITH_TONE_NUMBER | PinyinFormat.LOWERCASE | PinyinFormat.WITH_U_UNICODE;
            var pinyinText = Pinyin4Net.GetPinyin(normalizedText, format) ?? string.Empty;
            var syllables = pinyinText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var parts = new List<PinyinSyllableParts>(syllables.Length);
            foreach (var syllable in syllables)
            {
                parts.Add(SplitSyllable(ConvertToneNumberToToneMark(syllable)));
            }

            if (parts.Count == 0)
            {
                throw new InvalidOperationException($"无法为唤醒词生成拼音：{normalizedText}");
            }

            return parts;
        }

        private static PinyinSyllableParts SplitSyllable(string syllable)
        {
            if (string.IsNullOrWhiteSpace(syllable))
            {
                throw new InvalidOperationException("拼音 syllable 不能为空。");
            }

            foreach (var initial in Initials)
            {
                if (!syllable.StartsWith(initial, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var finalWithTone = syllable.Substring(initial.Length);
                if (finalWithTone.Length == 0)
                {
                    throw new InvalidOperationException($"无法拆分 syllable：{syllable}");
                }

                return new PinyinSyllableParts(initial, finalWithTone);
            }

            return new PinyinSyllableParts(string.Empty, syllable);
        }

        private static string ConvertToneNumberToToneMark(string syllable)
        {
            if (string.IsNullOrWhiteSpace(syllable))
            {
                throw new InvalidOperationException("拼音 syllable 不能为空。");
            }

            var toneDigit = syllable[syllable.Length - 1];
            if (toneDigit < '0' || toneDigit > '5')
            {
                return syllable;
            }

            var tone = toneDigit - '0';
            var plainSyllable = syllable.Substring(0, syllable.Length - 1).Replace('v', 'ü');
            if (tone == 0 || tone == 5)
            {
                return plainSyllable;
            }

            var markIndex = FindToneMarkIndex(plainSyllable);
            if (markIndex < 0)
            {
                return plainSyllable;
            }

            var vowel = plainSyllable[markIndex];
            if (!ToneMarks.TryGetValue(vowel, out var markedVowels))
            {
                return plainSyllable;
            }

            var builder = new StringBuilder(plainSyllable);
            builder[markIndex] = markedVowels[tone - 1];
            return builder.ToString();
        }

        private static int FindToneMarkIndex(string syllable)
        {
            var aIndex = syllable.IndexOf('a');
            if (aIndex >= 0)
            {
                return aIndex;
            }

            var eIndex = syllable.IndexOf('e');
            if (eIndex >= 0)
            {
                return eIndex;
            }

            var ouIndex = syllable.IndexOf("ou", StringComparison.Ordinal);
            if (ouIndex >= 0)
            {
                return ouIndex;
            }

            for (var index = syllable.Length - 1; index >= 0; index--)
            {
                var character = syllable[index];
                if (character == 'i' || character == 'o' || character == 'u' || character == 'ü')
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
