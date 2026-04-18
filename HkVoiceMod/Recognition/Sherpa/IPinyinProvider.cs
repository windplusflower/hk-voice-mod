using System.Collections.Generic;

namespace HkVoiceMod.Recognition.Sherpa
{
    public interface IPinyinProvider
    {
        IReadOnlyList<PinyinSyllableParts> ConvertToSyllables(string text);
    }
}
