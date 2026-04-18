using System.Collections.Generic;
using HkVoiceMod.Commands;

namespace HkVoiceMod.Recognition.Sherpa
{
    public sealed class SherpaKeywordArtifacts
    {
        public const string GeneratedDirectoryName = "generated";
        public const string RawKeywordsFileName = "keywords_raw.generated.txt";
        public const string KeywordsFileName = "keywords.generated.txt";

        public SherpaKeywordArtifacts(string rawKeywordsPath, string compiledKeywordsPath, IReadOnlyDictionary<string, VoiceCommand> keywordLookup)
        {
            RawKeywordsPath = rawKeywordsPath;
            CompiledKeywordsPath = compiledKeywordsPath;
            KeywordLookup = keywordLookup;
        }

        public string RawKeywordsPath { get; }

        public string CompiledKeywordsPath { get; }

        public IReadOnlyDictionary<string, VoiceCommand> KeywordLookup { get; }
    }
}
