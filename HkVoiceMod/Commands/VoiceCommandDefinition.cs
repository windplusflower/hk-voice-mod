namespace HkVoiceMod.Commands
{
    public sealed class VoiceCommandDefinition
    {
        public VoiceCommandDefinition(VoiceCommand command, string displayName, string defaultWakeWord, float defaultThreshold)
        {
            Command = command;
            DisplayName = displayName;
            DefaultWakeWord = defaultWakeWord;
            DefaultThreshold = defaultThreshold;
        }

        public VoiceCommand Command { get; }

        public string DisplayName { get; }

        public string DefaultWakeWord { get; }

        public float DefaultThreshold { get; }
    }
}
