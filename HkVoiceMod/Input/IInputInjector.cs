using HkVoiceMod.Commands;

namespace HkVoiceMod.Input
{
    public interface IInputInjector
    {
        void Dispatch(VoiceCommand command, float realtimeSinceStartup);

        void Tick(float unscaledDeltaTime, float realtimeSinceStartup);

        void ReleaseContinuousInputs();
    }
}
