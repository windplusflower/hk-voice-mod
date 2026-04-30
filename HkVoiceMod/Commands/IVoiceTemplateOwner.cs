using System.Collections.Generic;

namespace HkVoiceMod.Commands
{
    public interface IVoiceTemplateOwner
    {
        string TemplateOwnerId { get; }

        string TemplateDisplayName { get; }

        string WakeWord { get; set; }

        bool EnableTemplateVerification { get; set; }

        List<VoiceTemplateConfig> Templates { get; set; }
    }
}
