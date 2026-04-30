using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace HkVoiceMod.Recognition
{
    internal sealed class VoiceRecognitionTraceWriter : IDisposable
    {
        private readonly object _sync = new object();
        private readonly Action<string> _logWarn;
        private readonly string? _tracePath;
        private bool _writeFailed;

        public VoiceRecognitionTraceWriter(string assemblyDirectory, bool enabled, Action<string>? logWarn = null)
        {
            _logWarn = logWarn ?? (_ => { });
            if (!enabled || string.IsNullOrWhiteSpace(assemblyDirectory))
            {
                return;
            }

            var logsDirectory = Path.Combine(assemblyDirectory, "logs");
            Directory.CreateDirectory(logsDirectory);
            _tracePath = Path.Combine(logsDirectory, "voice-recognition-trace.tsv");

            if (!File.Exists(_tracePath))
            {
                File.WriteAllText(_tracePath, "timestamp_utc\tevent\tkeyword\toutcome\tsegment_id\tvoiced_ms\ttotal_ms\tpeak_rms\tnote" + Environment.NewLine, Encoding.UTF8);
            }
        }

        public void WriteKeywordDecision(DateTime timestampUtc, string keyword, string outcome, RecognitionGateDecision decision, string note)
        {
            WriteLine(timestampUtc, "keyword", keyword, outcome, decision.SegmentId, decision.VoicedMilliseconds, decision.TotalMilliseconds, decision.PeakRms, note);
        }

        public void WriteSegmentClosed(CompletedSpeechSegment segment)
        {
            if (segment == null)
            {
                throw new ArgumentNullException(nameof(segment));
            }

            WriteLine(segment.CompletedUtc, "segment", string.Empty, segment.HasAcceptedRecognition ? "closed-after-accept" : "closed-without-accept", segment.SegmentId, segment.VoicedMilliseconds, segment.TotalMilliseconds, segment.PeakRms, string.Empty);
        }

        public void Dispose()
        {
        }

        private void WriteLine(DateTime timestampUtc, string eventType, string keyword, string outcome, int segmentId, int voicedMilliseconds, int totalMilliseconds, float peakRms, string note)
        {
            if (_tracePath == null || _writeFailed)
            {
                return;
            }

            var line = string.Join(
                "\t",
                timestampUtc.ToString("O", CultureInfo.InvariantCulture),
                Sanitize(eventType),
                Sanitize(keyword),
                Sanitize(outcome),
                segmentId.ToString(CultureInfo.InvariantCulture),
                voicedMilliseconds.ToString(CultureInfo.InvariantCulture),
                totalMilliseconds.ToString(CultureInfo.InvariantCulture),
                peakRms.ToString("0.0000", CultureInfo.InvariantCulture),
                Sanitize(note));

            try
            {
                lock (_sync)
                {
                    File.AppendAllText(_tracePath, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                _writeFailed = true;
                _logWarn($"Failed to write voice recognition trace '{_tracePath}': {ex.Message}");
            }
        }

        private static string Sanitize(string text)
        {
            return (text ?? string.Empty).Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }
    }
}
