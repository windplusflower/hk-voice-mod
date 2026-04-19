using System;

namespace HkVoiceMod.Commands
{
    [Serializable]
    public sealed class VoiceMacroKeyEvent
    {
        public int DelayBeforeMilliseconds { get; set; }

        public global::GlobalEnums.HeroActionButton ActionButton { get; set; }

        public VoiceMacroKeyEventKind EventKind { get; set; }

        public string PairId { get; set; } = string.Empty;

        public VoiceMacroKeyEvent Clone()
        {
            return new VoiceMacroKeyEvent
            {
                DelayBeforeMilliseconds = DelayBeforeMilliseconds,
                ActionButton = ActionButton,
                EventKind = EventKind,
                PairId = PairId
            };
        }
    }
}
