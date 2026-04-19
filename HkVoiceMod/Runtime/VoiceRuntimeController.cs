using System;
using System.Collections.Concurrent;
using HkVoiceMod.Commands;
using HkVoiceMod.Input;
using HkVoiceMod.Recognition;
using HkVoiceMod.Recognition.Sherpa;
using UnityEngine;

namespace HkVoiceMod.Runtime
{
    public sealed class VoiceRuntimeController : MonoBehaviour
    {
        private readonly ConcurrentQueue<RecognizedTriggerEvent> _recognizedTriggers = new ConcurrentQueue<RecognizedTriggerEvent>();

        private HkVoiceMod? _mod;
        private VoiceModSettings _settings = new VoiceModSettings();
        private HeroActionInputInjector _inputInjector = new HeroActionInputInjector(new VoiceModSettings());
        private VoiceMacroRunner _macroRunner = new VoiceMacroRunner();
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
             _macroRunner.ApplySettings(_settings);
            RestartBackend();
        }

        private void Update()
        {
            DrainRecognizedTriggers();
            _macroRunner.Tick(Time.realtimeSinceStartup, _inputInjector);
            _inputInjector.Tick(Time.unscaledDeltaTime, Time.realtimeSinceStartup);
        }

        private void OnDestroy()
        {
            ShutdownBackend();
            _macroRunner.CancelPendingSteps();
            _inputInjector.ResetAllInputs(Time.realtimeSinceStartup);
        }

        private void DrainRecognizedTriggers()
        {
            var now = Time.realtimeSinceStartup;

            while (_recognizedTriggers.TryDequeue(out var triggerEvent))
            {
                if (_settings.LogRecognizedText)
                {
                    _mod?.LogDebug($"Recognized '{triggerEvent.RawText}' -> {triggerEvent.TriggerKind}:{triggerEvent.TriggerId} @ {triggerEvent.Timestamp:0.000}s");
                }

                if (triggerEvent.TriggerKind == VoiceTriggerKind.Stop)
                {
                    _macroRunner.CancelPendingSteps();
                    _inputInjector.ReleaseAllMacroActionButtons();
                    _inputInjector.ReleaseContinuousInputs();
                    continue;
                }

                var macro = FindMacroById(triggerEvent.TriggerId);
                if (macro == null)
                {
                    _mod?.LogWarn($"Ignored unknown macro trigger: {triggerEvent.TriggerId}");
                    continue;
                }

                _macroRunner.QueueMacro(macro, now);
            }
        }

        private VoiceMacroConfig? FindMacroById(string triggerId)
        {
            foreach (var macro in _settings.GetOrderedMacroConfigs())
            {
                if (string.Equals(macro.Id, triggerId, StringComparison.Ordinal))
                {
                    return macro;
                }
            }

            return null;
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
                backend.Start(_recognizedTriggers);
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
