namespace HkVoiceMod.Recognition.Sherpa
{
    public sealed class PinyinSyllableParts
    {
        public PinyinSyllableParts(string initial, string finalWithTone)
        {
            Initial = initial ?? string.Empty;
            FinalWithTone = finalWithTone ?? string.Empty;
        }

        public string Initial { get; }

        public string FinalWithTone { get; }
    }
}
