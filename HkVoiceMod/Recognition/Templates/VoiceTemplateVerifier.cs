using System;
using System.Collections.Generic;
using HkVoiceMod.Commands;
using NAudio.Wave;

namespace HkVoiceMod.Recognition.Templates
{
    internal sealed class VoiceTemplateVerifier
    {
        private const int FrameSizeSamples = 320;
        private const int FrameHopSamples = 160;
        private const float MaxAcceptedDistance = 0.22f;
        private const float MinMarginDistance = 0.015f;

        private readonly Dictionary<string, List<PreparedVoiceTemplate>> _templatesByTriggerId;
        private readonly List<PreparedVoiceTemplate> _allTemplates;

        private VoiceTemplateVerifier(Dictionary<string, List<PreparedVoiceTemplate>> templatesByTriggerId, List<PreparedVoiceTemplate> allTemplates)
        {
            _templatesByTriggerId = templatesByTriggerId;
            _allTemplates = allTemplates;
        }

        public static VoiceTemplateVerifier Load(string assemblyDirectory, VoiceModSettings settings)
        {
            var templatesByTriggerId = new Dictionary<string, List<PreparedVoiceTemplate>>(StringComparer.Ordinal);
            var allTemplates = new List<PreparedVoiceTemplate>();

            LoadOwnerTemplates(assemblyDirectory, settings.StopKeywordConfig, "stop", templatesByTriggerId, allTemplates);

            foreach (var macro in settings.GetOrderedMacroConfigs())
            {
                LoadOwnerTemplates(assemblyDirectory, macro, macro.Id, templatesByTriggerId, allTemplates);
            }

            return new VoiceTemplateVerifier(templatesByTriggerId, allTemplates);
        }

        public TemplateVerificationDecision Verify(string triggerId, float[] segmentSamples)
        {
            if (!_templatesByTriggerId.TryGetValue(triggerId, out var candidateTemplates) || candidateTemplates.Count == 0)
            {
                return TemplateVerificationDecision.CreateSkipped("no-templates-for-trigger");
            }

            var features = ExtractFeatures(segmentSamples);
            if (features.Count == 0)
            {
                return TemplateVerificationDecision.CreateRejected("empty-segment-features", float.MaxValue, float.MaxValue, 0);
            }

            var hasBestTemplate = false;
            var hasSecondTemplate = false;
            PreparedVoiceTemplate bestTemplate = default;
            PreparedVoiceTemplate secondTemplate = default;
            var bestDistance = float.MaxValue;
            var secondDistance = float.MaxValue;
            for (var index = 0; index < _allTemplates.Count; index++)
            {
                var template = _allTemplates[index];
                var distance = ComputeDtwDistance(features, template.Features);
                if (distance < bestDistance)
                {
                    secondDistance = bestDistance;
                    secondTemplate = bestTemplate;
                    hasSecondTemplate = hasBestTemplate;
                    bestDistance = distance;
                    bestTemplate = template;
                    hasBestTemplate = true;
                }
                else if (distance < secondDistance)
                {
                    secondDistance = distance;
                    secondTemplate = template;
                    hasSecondTemplate = true;
                }
            }

            if (!hasBestTemplate)
            {
                return TemplateVerificationDecision.CreateSkipped("no-loaded-templates");
            }

            if (!string.Equals(bestTemplate.TriggerId, triggerId, StringComparison.Ordinal))
            {
                return TemplateVerificationDecision.CreateRejected("best-template-belongs-to-other-trigger", bestDistance, secondDistance, _allTemplates.Count);
            }

            if (bestDistance > MaxAcceptedDistance)
            {
                return TemplateVerificationDecision.CreateRejected("distance-too-large", bestDistance, secondDistance, _allTemplates.Count);
            }

            if (hasSecondTemplate
                && !string.Equals(secondTemplate.TriggerId, triggerId, StringComparison.Ordinal)
                && secondDistance - bestDistance < MinMarginDistance)
            {
                return TemplateVerificationDecision.CreateRejected("distance-margin-too-small", bestDistance, secondDistance, _allTemplates.Count);
            }

            return TemplateVerificationDecision.CreateAccepted(bestDistance, secondDistance, _allTemplates.Count);
        }

        private static float[] ReadTemplateSamples(string filePath)
        {
            using (var reader = new WaveFileReader(filePath))
            {
                var sampleCount = (int)(reader.Length / 2);
                var buffer = new byte[reader.Length];
                var bytesRead = reader.Read(buffer, 0, buffer.Length);
                var samples = new float[bytesRead / 2];
                for (var index = 0; index < samples.Length; index++)
                {
                    var offset = index * 2;
                    short sample = (short)(buffer[offset] | (buffer[offset + 1] << 8));
                    samples[index] = sample / 32768f;
                }

                return samples;
            }
        }

        private static void LoadOwnerTemplates(
            string assemblyDirectory,
            IVoiceTemplateOwner? owner,
            string triggerId,
            Dictionary<string, List<PreparedVoiceTemplate>> templatesByTriggerId,
            List<PreparedVoiceTemplate> allTemplates)
        {
            if (owner == null || !owner.EnableTemplateVerification || owner.Templates == null || owner.Templates.Count == 0)
            {
                return;
            }

            var normalizedWakeWord = VoiceModSettings.NormalizeWakeWord(owner.WakeWord);
            for (var index = 0; index < owner.Templates.Count; index++)
            {
                var template = owner.Templates[index];
                if (template == null
                    || !template.Enabled
                    || !string.Equals(VoiceModSettings.NormalizeWakeWord(template.RecordedWakeWord), normalizedWakeWord, StringComparison.Ordinal)
                    || !VoiceTemplateStorage.TemplateFileExists(assemblyDirectory, template))
                {
                    continue;
                }

                var samples = ReadTemplateSamples(VoiceTemplateStorage.ResolveTemplateFilePath(assemblyDirectory, template.RelativePath));
                var features = ExtractFeatures(samples);
                if (features.Count == 0)
                {
                    continue;
                }

                var preparedTemplate = new PreparedVoiceTemplate(triggerId, template.TemplateId, features);
                if (!templatesByTriggerId.TryGetValue(triggerId, out var list))
                {
                    list = new List<PreparedVoiceTemplate>();
                    templatesByTriggerId[triggerId] = list;
                }

                list.Add(preparedTemplate);
                allTemplates.Add(preparedTemplate);
            }
        }

        private static List<TemplateFeatureFrame> ExtractFeatures(float[] samples)
        {
            var features = new List<TemplateFeatureFrame>();
            if (samples == null || samples.Length < FrameSizeSamples)
            {
                return features;
            }

            var previousRms = 0f;
            for (var start = 0; start + FrameSizeSamples <= samples.Length; start += FrameHopSamples)
            {
                var rms = 0f;
                var zeroCrossings = 0;
                float previousSample = 0f;
                for (var index = start; index < start + FrameSizeSamples; index++)
                {
                    var sample = samples[index];
                    rms += sample * sample;
                    if (index > start && ((sample >= 0f && previousSample < 0f) || (sample < 0f && previousSample >= 0f)))
                    {
                        zeroCrossings++;
                    }

                    previousSample = sample;
                }

                rms = (float)Math.Sqrt(rms / FrameSizeSamples);
                var zcr = zeroCrossings / (float)FrameSizeSamples;
                var deltaRms = rms - previousRms;
                previousRms = rms;
                features.Add(new TemplateFeatureFrame(rms, zcr, deltaRms));
            }

            return features;
        }

        private static float ComputeDtwDistance(IReadOnlyList<TemplateFeatureFrame> candidate, IReadOnlyList<TemplateFeatureFrame> template)
        {
            var rows = candidate.Count;
            var cols = template.Count;
            if (rows == 0 || cols == 0)
            {
                return float.MaxValue;
            }

            var cost = new float[rows + 1, cols + 1];
            for (var i = 0; i <= rows; i++)
            {
                for (var j = 0; j <= cols; j++)
                {
                    cost[i, j] = float.MaxValue;
                }
            }

            cost[0, 0] = 0f;
            for (var i = 1; i <= rows; i++)
            {
                for (var j = 1; j <= cols; j++)
                {
                    var frameDistance = candidate[i - 1].DistanceTo(template[j - 1]);
                    var bestPrevious = Math.Min(cost[i - 1, j], Math.Min(cost[i, j - 1], cost[i - 1, j - 1]));
                    cost[i, j] = frameDistance + bestPrevious;
                }
            }

            return cost[rows, cols] / (rows + cols);
        }

        private readonly struct PreparedVoiceTemplate
        {
            public PreparedVoiceTemplate(string triggerId, string templateId, List<TemplateFeatureFrame> features)
            {
                TriggerId = triggerId;
                TemplateId = templateId;
                Features = features;
            }

            public string TriggerId { get; }

            public string TemplateId { get; }

            public List<TemplateFeatureFrame> Features { get; }
        }

        private readonly struct TemplateFeatureFrame
        {
            public TemplateFeatureFrame(float rms, float zeroCrossingRate, float deltaRms)
            {
                Rms = rms;
                ZeroCrossingRate = zeroCrossingRate;
                DeltaRms = deltaRms;
            }

            public float Rms { get; }

            public float ZeroCrossingRate { get; }

            public float DeltaRms { get; }

            public float DistanceTo(TemplateFeatureFrame other)
            {
                var deltaRms = Rms - other.Rms;
                var deltaZcr = ZeroCrossingRate - other.ZeroCrossingRate;
                var deltaEnergy = DeltaRms - other.DeltaRms;
                return (float)Math.Sqrt(deltaRms * deltaRms + deltaZcr * deltaZcr + deltaEnergy * deltaEnergy);
            }
        }
    }

    internal readonly struct TemplateVerificationDecision
    {
        private TemplateVerificationDecision(bool hasUsableTemplates, bool accepted, string reason, float bestDistance, float secondDistance, int comparedTemplateCount)
        {
            HasUsableTemplates = hasUsableTemplates;
            Accepted = accepted;
            Reason = reason ?? string.Empty;
            BestDistance = bestDistance;
            SecondDistance = secondDistance;
            ComparedTemplateCount = comparedTemplateCount;
        }

        public bool HasUsableTemplates { get; }

        public bool Accepted { get; }

        public string Reason { get; }

        public float BestDistance { get; }

        public float SecondDistance { get; }

        public int ComparedTemplateCount { get; }

        public static TemplateVerificationDecision CreateSkipped(string reason)
        {
            return new TemplateVerificationDecision(false, true, reason, float.MaxValue, float.MaxValue, 0);
        }

        public static TemplateVerificationDecision CreateAccepted(float bestDistance, float secondDistance, int comparedTemplateCount)
        {
            return new TemplateVerificationDecision(true, true, "accepted", bestDistance, secondDistance, comparedTemplateCount);
        }

        public static TemplateVerificationDecision CreateRejected(string reason, float bestDistance, float secondDistance, int comparedTemplateCount)
        {
            return new TemplateVerificationDecision(true, false, reason, bestDistance, secondDistance, comparedTemplateCount);
        }
    }
}
