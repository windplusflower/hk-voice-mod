using System;
using System.Collections.Concurrent;
using HkVoiceMod.Input;
using HkVoiceMod.Recognition;
using HkVoiceMod.Recognition.Sherpa;
using UnityEngine;

namespace HkVoiceMod.Runtime
{
    public sealed class VoiceRuntimeController : MonoBehaviour
    {
        private readonly ConcurrentQueue<RecognizedCommandEvent> _recognizedCommands = new ConcurrentQueue<RecognizedCommandEvent>();

        private HkVoiceMod? _mod;
        private VoiceModSettings _settings = new VoiceModSettings();
        private HeroActionInputInjector _inputInjector = new HeroActionInputInjector(new VoiceModSettings());
        private IVoiceRecognitionBackend? _backend;

        public void Initialize(HkVoiceMod mod, VoiceModSettings settings)
        {
            _mod = mod ?? throw new ArgumentNullException(nameof(mod));
            ApplySettings(settings);
        }

        public void ApplySettings(VoiceModSettings settings)
        {
            _settings = settings?.Clone() ?? new VoiceModSettings();
            _inputInjector.ApplySettings(_settings);
            RestartBackend();
        }

        private void Update()
        {
            DrainRecognizedCommands();
            _inputInjector.Tick(Time.unscaledDeltaTime, Time.realtimeSinceStartup);
        }

        private void OnDestroy()
        {
            ShutdownBackend();
            _inputInjector.ResetAllInputs(Time.realtimeSinceStartup);
        }

        private void DrainRecognizedCommands()
        {
            var now = Time.realtimeSinceStartup;

            while (_recognizedCommands.TryDequeue(out var commandEvent))
            {
                if (_settings.LogRecognizedText)
                {
                    _mod?.LogDebug($"Recognized '{commandEvent.RawText}' -> {commandEvent.Command} @ {commandEvent.Timestamp:0.000}s");
                }

                _inputInjector.Dispatch(commandEvent.Command, now);
            }
        }

        private void RestartBackend()
        {
            ShutdownBackend();

            if (!_settings.Enabled)
            {
                _mod?.LogInfo("Voice backend disabled by settings.");
                _inputInjector.ResetAllInputs(Time.realtimeSinceStartup);
                return;
            }

            var backend = new SherpaKeywordSpotterBackend(
                _settings,
                message => _mod?.LogDebug(message),
                message => _mod?.LogInfo(message),
                message => _mod?.LogWarn(message),
                message => _mod?.LogError(message));

            try
            {
                backend.Start(_recognizedCommands);
                _backend = backend;
                _mod?.LogInfo("Voice backend started.");
            }
            catch (Exception ex)
            {
                try
                {
                    backend.Dispose();
                }
                catch (Exception disposeEx)
                {
                    _mod?.LogWarn($"Voice backend cleanup after failed startup raised an exception: {disposeEx}");
                }

                _mod?.LogError($"Failed to start voice backend: {ex}");
            }
        }

        private void ShutdownBackend()
        {
            if (_backend == null)
            {
                return;
            }

            try
            {
                _backend.Stop();
                _backend.Dispose();
            }
            catch (Exception ex)
            {
                _mod?.LogWarn($"Voice backend shutdown raised an exception: {ex}");
            }
            finally
            {
                _backend = null;
            }
        }
    }
}
