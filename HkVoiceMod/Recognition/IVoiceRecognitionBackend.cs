using System;
using System.Collections.Concurrent;

namespace HkVoiceMod.Recognition
{
    public interface IVoiceRecognitionBackend : IDisposable
    {
        bool IsRunning { get; }

        void Start(ConcurrentQueue<RecognizedCommandEvent> outputQueue);

        void Stop();
    }
}
