namespace HkVoiceMod
{
    public sealed class ApplyVoiceSettingsResult
    {
        private ApplyVoiceSettingsResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public bool Success { get; }

        public string Message { get; }

        public static ApplyVoiceSettingsResult CreateSuccess(string message)
        {
            return new ApplyVoiceSettingsResult(true, message);
        }

        public static ApplyVoiceSettingsResult CreateFailure(string message)
        {
            return new ApplyVoiceSettingsResult(false, message);
        }
    }
}
