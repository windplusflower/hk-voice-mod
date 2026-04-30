using System;

namespace HkVoiceMod.Commands
{
    [Serializable]
    public sealed class VoiceTemplateConfig
    {
        public string TemplateId { get; set; } = string.Empty;

        public string RelativePath { get; set; } = string.Empty;

        public string RecordedWakeWord { get; set; } = string.Empty;

        public bool Enabled { get; set; } = true;

        public long CreatedUtcTicks { get; set; }

        public VoiceTemplateConfig Clone()
        {
            return new VoiceTemplateConfig
            {
                TemplateId = TemplateId,
                RelativePath = RelativePath,
                RecordedWakeWord = RecordedWakeWord,
                Enabled = Enabled,
                CreatedUtcTicks = CreatedUtcTicks
            };
        }
    }
}
