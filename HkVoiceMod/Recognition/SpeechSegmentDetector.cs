using System;

namespace HkVoiceMod.Recognition
{
    internal sealed class SpeechSegmentDetector
    {
        private readonly bool _enabled;
        private readonly int _sampleRateHz;
        private readonly float _voiceActivityRmsThreshold;
        private readonly int _minimumSpeechMilliseconds;
        private readonly int _endpointSilenceMilliseconds;
        private readonly int _postSpeechAcceptanceMilliseconds;

        private ActiveSpeechSegment? _activeSegment;
        private CompletedSpeechSegment? _recentCompletedSegment;
        private int _nextSegmentId = 1;

        public SpeechSegmentDetector(VoiceModSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _enabled = settings.EnableSpeechSegmentation;
            _sampleRateHz = settings.SampleRateHz;
            _voiceActivityRmsThreshold = settings.VoiceActivityRmsThreshold;
            _minimumSpeechMilliseconds = settings.MinimumSpeechMilliseconds;
            _endpointSilenceMilliseconds = settings.EndpointSilenceMilliseconds;
            _postSpeechAcceptanceMilliseconds = settings.PostSpeechAcceptanceMilliseconds;
        }

        public SpeechFrameAnalysis AnalyzeFrame(float[] samples, DateTime analysisUtc)
        {
            if (samples == null)
            {
                throw new ArgumentNullException(nameof(samples));
            }

            var rms = ComputeRms(samples);
            if (!_enabled)
            {
                return new SpeechFrameAnalysis(false, false, false, null, rms);
            }

            var frameMilliseconds = ResolveFrameDurationMilliseconds(samples.Length);
            var isSpeechFrame = rms >= _voiceActivityRmsThreshold;
            var segmentStarted = false;
            CompletedSpeechSegment? completedSegment = null;

            if (isSpeechFrame)
            {
                if (_activeSegment == null)
                {
                    _activeSegment = new ActiveSpeechSegment(_nextSegmentId++, analysisUtc);
                    segmentStarted = true;
                }

                _activeSegment.TotalMilliseconds += frameMilliseconds;
                _activeSegment.VoicedMilliseconds += frameMilliseconds;
                _activeSegment.TrailingSilenceMilliseconds = 0;
                if (rms > _activeSegment.PeakRms)
                {
                    _activeSegment.PeakRms = rms;
                }
            }
            else if (_activeSegment != null)
            {
                _activeSegment.TotalMilliseconds += frameMilliseconds;
                _activeSegment.TrailingSilenceMilliseconds += frameMilliseconds;
                if (rms > _activeSegment.PeakRms)
                {
                    _activeSegment.PeakRms = rms;
                }

                if (_activeSegment.TrailingSilenceMilliseconds >= _endpointSilenceMilliseconds)
                {
                    completedSegment = FinalizeActiveSegment(analysisUtc);
                }
            }

            return new SpeechFrameAnalysis(segmentStarted, completedSegment != null, _activeSegment != null, completedSegment, rms);
        }

        public RecognitionGateDecision EvaluateRecognition(DateTime recognitionUtc)
        {
            if (!_enabled)
            {
                return RecognitionGateDecision.CreateAccepted("speech-gate-disabled", 0, 0, 0, 0f);
            }

            PruneExpiredRecentSegment(recognitionUtc);

            if (TryAcceptActiveSegment(out var activeDecision))
            {
                return activeDecision;
            }

            if (TryAcceptRecentSegment(recognitionUtc, out var recentDecision))
            {
                return recentDecision;
            }

            if (_activeSegment != null)
            {
                if (_activeSegment.HasAcceptedRecognition)
                {
                    return RecognitionGateDecision.CreateRejected("segment-already-accepted", _activeSegment.SegmentId, _activeSegment.VoicedMilliseconds, _activeSegment.TotalMilliseconds, _activeSegment.PeakRms);
                }

                return RecognitionGateDecision.CreateRejected("speech-too-short", _activeSegment.SegmentId, _activeSegment.VoicedMilliseconds, _activeSegment.TotalMilliseconds, _activeSegment.PeakRms);
            }

            if (_recentCompletedSegment != null)
            {
                if (_recentCompletedSegment.HasAcceptedRecognition)
                {
                    return RecognitionGateDecision.CreateRejected("segment-already-accepted", _recentCompletedSegment.SegmentId, _recentCompletedSegment.VoicedMilliseconds, _recentCompletedSegment.TotalMilliseconds, _recentCompletedSegment.PeakRms);
                }

                return RecognitionGateDecision.CreateRejected("speech-too-short", _recentCompletedSegment.SegmentId, _recentCompletedSegment.VoicedMilliseconds, _recentCompletedSegment.TotalMilliseconds, _recentCompletedSegment.PeakRms);
            }

            return RecognitionGateDecision.CreateRejected("no-speech-segment", 0, 0, 0, 0f);
        }

        private bool TryAcceptActiveSegment(out RecognitionGateDecision decision)
        {
            if (_activeSegment != null && !_activeSegment.HasAcceptedRecognition && _activeSegment.VoicedMilliseconds >= _minimumSpeechMilliseconds)
            {
                _activeSegment.HasAcceptedRecognition = true;
                decision = RecognitionGateDecision.CreateAccepted("active-segment", _activeSegment.SegmentId, _activeSegment.VoicedMilliseconds, _activeSegment.TotalMilliseconds, _activeSegment.PeakRms);
                return true;
            }

            decision = RecognitionGateDecision.CreateRejected("no-active-segment", 0, 0, 0, 0f);
            return false;
        }

        private bool TryAcceptRecentSegment(DateTime recognitionUtc, out RecognitionGateDecision decision)
        {
            if (_recentCompletedSegment != null
                && !_recentCompletedSegment.HasAcceptedRecognition
                && _recentCompletedSegment.VoicedMilliseconds >= _minimumSpeechMilliseconds
                && recognitionUtc - _recentCompletedSegment.CompletedUtc <= TimeSpan.FromMilliseconds(_postSpeechAcceptanceMilliseconds))
            {
                _recentCompletedSegment.HasAcceptedRecognition = true;
                decision = RecognitionGateDecision.CreateAccepted("recent-segment", _recentCompletedSegment.SegmentId, _recentCompletedSegment.VoicedMilliseconds, _recentCompletedSegment.TotalMilliseconds, _recentCompletedSegment.PeakRms);
                return true;
            }

            decision = RecognitionGateDecision.CreateRejected("no-recent-segment", 0, 0, 0, 0f);
            return false;
        }

        private CompletedSpeechSegment FinalizeActiveSegment(DateTime completedUtc)
        {
            if (_activeSegment == null)
            {
                throw new InvalidOperationException("Cannot finalize a speech segment when none is active.");
            }

            var completedSegment = new CompletedSpeechSegment(
                _activeSegment.SegmentId,
                _activeSegment.StartedUtc,
                completedUtc,
                _activeSegment.TotalMilliseconds,
                _activeSegment.VoicedMilliseconds,
                _activeSegment.PeakRms,
                _activeSegment.HasAcceptedRecognition);

            _recentCompletedSegment = completedSegment;
            _activeSegment = null;
            return completedSegment;
        }

        private void PruneExpiredRecentSegment(DateTime utcNow)
        {
            if (_recentCompletedSegment == null)
            {
                return;
            }

            if (utcNow - _recentCompletedSegment.CompletedUtc > TimeSpan.FromMilliseconds(_postSpeechAcceptanceMilliseconds))
            {
                _recentCompletedSegment = null;
            }
        }

        private int ResolveFrameDurationMilliseconds(int sampleCount)
        {
            var durationMilliseconds = (int)Math.Round(sampleCount * 1000d / _sampleRateHz, MidpointRounding.AwayFromZero);
            return Math.Max(1, durationMilliseconds);
        }

        private static float ComputeRms(float[] samples)
        {
            if (samples.Length == 0)
            {
                return 0f;
            }

            double sumSquares = 0d;
            for (var index = 0; index < samples.Length; index++)
            {
                var sample = samples[index];
                sumSquares += sample * sample;
            }

            return (float)Math.Sqrt(sumSquares / samples.Length);
        }

        private sealed class ActiveSpeechSegment
        {
            public ActiveSpeechSegment(int segmentId, DateTime startedUtc)
            {
                SegmentId = segmentId;
                StartedUtc = startedUtc;
            }

            public int SegmentId { get; }

            public DateTime StartedUtc { get; }

            public int TotalMilliseconds { get; set; }

            public int VoicedMilliseconds { get; set; }

            public int TrailingSilenceMilliseconds { get; set; }

            public float PeakRms { get; set; }

            public bool HasAcceptedRecognition { get; set; }
        }
    }

    internal sealed class SpeechFrameAnalysis
    {
        public SpeechFrameAnalysis(bool segmentStarted, bool segmentEnded, bool segmentActive, CompletedSpeechSegment? completedSegment, float rms)
        {
            SegmentStarted = segmentStarted;
            SegmentEnded = segmentEnded;
            SegmentActive = segmentActive;
            CompletedSegment = completedSegment;
            Rms = rms;
        }

        public bool SegmentStarted { get; }

        public bool SegmentEnded { get; }

        public bool SegmentActive { get; }

        public CompletedSpeechSegment? CompletedSegment { get; }

        public float Rms { get; }
    }

    internal sealed class CompletedSpeechSegment
    {
        public CompletedSpeechSegment(int segmentId, DateTime startedUtc, DateTime completedUtc, int totalMilliseconds, int voicedMilliseconds, float peakRms, bool hasAcceptedRecognition)
        {
            SegmentId = segmentId;
            StartedUtc = startedUtc;
            CompletedUtc = completedUtc;
            TotalMilliseconds = totalMilliseconds;
            VoicedMilliseconds = voicedMilliseconds;
            PeakRms = peakRms;
            HasAcceptedRecognition = hasAcceptedRecognition;
        }

        public int SegmentId { get; }

        public DateTime StartedUtc { get; }

        public DateTime CompletedUtc { get; }

        public int TotalMilliseconds { get; }

        public int VoicedMilliseconds { get; }

        public float PeakRms { get; }

        public bool HasAcceptedRecognition { get; set; }
    }

    internal readonly struct RecognitionGateDecision
    {
        public RecognitionGateDecision(bool accepted, string reason, int segmentId, int voicedMilliseconds, int totalMilliseconds, float peakRms)
        {
            Accepted = accepted;
            Reason = reason ?? string.Empty;
            SegmentId = segmentId;
            VoicedMilliseconds = voicedMilliseconds;
            TotalMilliseconds = totalMilliseconds;
            PeakRms = peakRms;
        }

        public bool Accepted { get; }

        public string Reason { get; }

        public int SegmentId { get; }

        public int VoicedMilliseconds { get; }

        public int TotalMilliseconds { get; }

        public float PeakRms { get; }

        public static RecognitionGateDecision CreateAccepted(string reason, int segmentId, int voicedMilliseconds, int totalMilliseconds, float peakRms)
        {
            return new RecognitionGateDecision(true, reason, segmentId, voicedMilliseconds, totalMilliseconds, peakRms);
        }

        public static RecognitionGateDecision CreateRejected(string reason, int segmentId, int voicedMilliseconds, int totalMilliseconds, float peakRms)
        {
            return new RecognitionGateDecision(false, reason, segmentId, voicedMilliseconds, totalMilliseconds, peakRms);
        }
    }
}
