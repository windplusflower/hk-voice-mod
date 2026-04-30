using System;
using System.Collections.Generic;
using System.IO;
using HkVoiceMod.Commands;
using NAudio.Wave;

namespace HkVoiceMod.Recognition.Templates
{
    internal static class VoiceTemplateStorage
    {
        private const string RootDirectoryName = "user-data";
        private const string TemplatesDirectoryName = "voice-templates";

        public static string ResolveTemplateRoot(string assemblyDirectory)
        {
            if (string.IsNullOrWhiteSpace(assemblyDirectory))
            {
                throw new ArgumentException("Assembly directory is required.", nameof(assemblyDirectory));
            }

            return Path.Combine(assemblyDirectory, RootDirectoryName, TemplatesDirectoryName);
        }

        public static VoiceTemplateConfig SaveTemplate(string assemblyDirectory, IVoiceTemplateOwner owner, string recordedWakeWord, byte[] pcmBytes, int sampleRateHz)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (pcmBytes == null || pcmBytes.Length == 0)
            {
                throw new ArgumentException("Template audio is empty.", nameof(pcmBytes));
            }

            var templateId = Guid.NewGuid().ToString("N");
            var ownerDirectory = Path.Combine(ResolveTemplateRoot(assemblyDirectory), owner.TemplateOwnerId);
            Directory.CreateDirectory(ownerDirectory);

            var fileName = templateId + ".wav";
            var fullPath = Path.Combine(ownerDirectory, fileName);
            using (var writer = new WaveFileWriter(fullPath, new WaveFormat(sampleRateHz, 16, 1)))
            {
                writer.Write(pcmBytes, 0, pcmBytes.Length);
            }

            return new VoiceTemplateConfig
            {
                TemplateId = templateId,
                RelativePath = BuildRelativePath(owner.TemplateOwnerId, fileName),
                RecordedWakeWord = VoiceModSettings.NormalizeWakeWord(recordedWakeWord),
                Enabled = true,
                CreatedUtcTicks = DateTime.UtcNow.Ticks
            };
        }

        public static void OverwriteTemplate(string assemblyDirectory, VoiceTemplateConfig template, string ownerId, string recordedWakeWord, byte[] pcmBytes, int sampleRateHz)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            if (pcmBytes == null || pcmBytes.Length == 0)
            {
                throw new ArgumentException("Template audio is empty.", nameof(pcmBytes));
            }

            var fullPath = ResolveTemplateFilePath(assemblyDirectory, template.RelativePath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var writer = new WaveFileWriter(fullPath, new WaveFormat(sampleRateHz, 16, 1)))
            {
                writer.Write(pcmBytes, 0, pcmBytes.Length);
            }

            template.RecordedWakeWord = VoiceModSettings.NormalizeWakeWord(recordedWakeWord);
            template.Enabled = true;
            template.CreatedUtcTicks = DateTime.UtcNow.Ticks;
            if (string.IsNullOrWhiteSpace(template.RelativePath))
            {
                var fileName = (string.IsNullOrWhiteSpace(template.TemplateId) ? Guid.NewGuid().ToString("N") : template.TemplateId) + ".wav";
                template.RelativePath = BuildRelativePath(ownerId, fileName);
            }
        }

        public static string ResolveTemplateFilePath(string assemblyDirectory, string relativePath)
        {
            var sanitizedRelativePath = (relativePath ?? string.Empty).Trim().Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(ResolveTemplateRoot(assemblyDirectory), sanitizedRelativePath));
        }

        public static bool TemplateFileExists(string assemblyDirectory, VoiceTemplateConfig template)
        {
            return template != null
                && !string.IsNullOrWhiteSpace(template.RelativePath)
                && File.Exists(ResolveTemplateFilePath(assemblyDirectory, template.RelativePath));
        }

        public static void CleanupUnreferencedTemplates(string assemblyDirectory, VoiceModSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var templateRoot = ResolveTemplateRoot(assemblyDirectory);
            if (!Directory.Exists(templateRoot))
            {
                return;
            }

            var referencedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var macro in settings.GetOrderedMacroConfigs())
            {
                if (macro?.Templates == null)
                {
                    continue;
                }

                foreach (var template in macro.Templates)
                {
                    if (template == null || string.IsNullOrWhiteSpace(template.RelativePath))
                    {
                        continue;
                    }

                    referencedPaths.Add(ResolveTemplateFilePath(assemblyDirectory, template.RelativePath));
                }
            }

            if (settings.StopKeywordConfig?.Templates != null)
            {
                foreach (var template in settings.StopKeywordConfig.Templates)
                {
                    if (template == null || string.IsNullOrWhiteSpace(template.RelativePath))
                    {
                        continue;
                    }

                    referencedPaths.Add(ResolveTemplateFilePath(assemblyDirectory, template.RelativePath));
                }
            }

            foreach (var filePath in Directory.GetFiles(templateRoot, "*.wav", SearchOption.AllDirectories))
            {
                if (!referencedPaths.Contains(Path.GetFullPath(filePath)))
                {
                    File.Delete(filePath);
                }
            }
        }

        private static string BuildRelativePath(string macroId, string fileName)
        {
            return $"{macroId}/{fileName}";
        }
    }
}
