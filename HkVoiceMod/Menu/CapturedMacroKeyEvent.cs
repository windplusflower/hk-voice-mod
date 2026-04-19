using HkVoiceMod.Commands;

namespace HkVoiceMod.Menu
{
    internal readonly struct CapturedMacroKeyEvent
    {
        public CapturedMacroKeyEvent(global::GlobalEnums.HeroActionButton actionButton, VoiceMacroKeyEventKind eventKind, int delayBeforeMilliseconds, string pairId)
        {
            ActionButton = actionButton;
            EventKind = eventKind;
            DelayBeforeMilliseconds = delayBeforeMilliseconds;
            PairId = pairId ?? string.Empty;
        }

        public global::GlobalEnums.HeroActionButton ActionButton { get; }

        public VoiceMacroKeyEventKind EventKind { get; }

        public int DelayBeforeMilliseconds { get; }

        public string PairId { get; }
    }
}
