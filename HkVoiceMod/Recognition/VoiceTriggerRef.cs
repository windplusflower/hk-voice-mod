using System;

namespace HkVoiceMod.Recognition
{
    public sealed class VoiceTriggerRef
    {
        public VoiceTriggerRef(VoiceTriggerKind triggerKind, string triggerId)
        {
            if (string.IsNullOrWhiteSpace(triggerId))
            {
                throw new ArgumentException("TriggerId is required.", nameof(triggerId));
            }

            TriggerKind = triggerKind;
            TriggerId = triggerId;
        }

        public VoiceTriggerKind TriggerKind { get; }

        public string TriggerId { get; }
    }
}
