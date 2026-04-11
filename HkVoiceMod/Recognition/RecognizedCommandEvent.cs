using HkVoiceMod.Commands;

namespace HkVoiceMod.Recognition
{
    public sealed class RecognizedCommandEvent
    {
        public RecognizedCommandEvent(VoiceCommand command, string rawText, float timestamp)
        {
            Command = command;
            RawText = rawText;
            Timestamp = timestamp;
        }

        public VoiceCommand Command { get; }

        public string RawText { get; }

        public float Timestamp { get; }
    }
}
