using System;
using System.Collections.Generic;
using System.Linq;

namespace HkVoiceMod.Commands
{
    public static class VoiceCommandCatalog
    {
        private static readonly VoiceCommandDefinition[] Definitions =
        {
            new VoiceCommandDefinition(VoiceCommand.Up, "上移", "往上", 0.1f),
            new VoiceCommandDefinition(VoiceCommand.Down, "下移", "往下", 0.1f),
            new VoiceCommandDefinition(VoiceCommand.Left, "左移", "往左", 0.1f),
            new VoiceCommandDefinition(VoiceCommand.Right, "右移", "往右", 0.1f),
            new VoiceCommandDefinition(VoiceCommand.Attack, "攻击", "攻击", 0.1f),
            new VoiceCommandDefinition(VoiceCommand.Jump, "跳跃", "跳跃", 0.1f),
            new VoiceCommandDefinition(VoiceCommand.Dash, "冲刺", "冲刺", 0.1f),
            new VoiceCommandDefinition(VoiceCommand.Howl, "上吼", "上吼", 0.1f),
            new VoiceCommandDefinition(VoiceCommand.Dive, "下砸", "下砸", 0.1f),
            new VoiceCommandDefinition(VoiceCommand.Cast, "法术", "法术", 0.1f),
            new VoiceCommandDefinition(VoiceCommand.Stop, "停止", "停止", 0.1f)
        };

        public static IReadOnlyList<VoiceCommandDefinition> All => Definitions;

        public static List<VoiceCommandKeywordConfig> CreateDefaultKeywordConfigs()
        {
            return Definitions
                .Select(definition => new VoiceCommandKeywordConfig
                {
                    Command = definition.Command,
                    WakeWord = definition.DefaultWakeWord,
                    KeywordThreshold = definition.DefaultThreshold
                })
                .ToList();
        }

        public static VoiceCommandDefinition GetDefinition(VoiceCommand command)
        {
            foreach (var definition in Definitions)
            {
                if (definition.Command == command)
                {
                    return definition;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(command), command, "Unsupported voice command definition.");
        }
    }
}
