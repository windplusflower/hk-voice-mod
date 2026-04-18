namespace HkVoiceMod.Recognition
{
    public sealed class RecognizedTriggerEvent
    {
        public RecognizedTriggerEvent(VoiceTriggerKind triggerKind, string triggerId, string rawText, float timestamp)
        {
            TriggerKind = triggerKind;
            TriggerId = triggerId;
            RawText = rawText;
            Timestamp = timestamp;
        }

        public VoiceTriggerKind TriggerKind { get; }

        public string TriggerId { get; }

        public string RawText { get; }

        public float Timestamp { get; }
    }
}
