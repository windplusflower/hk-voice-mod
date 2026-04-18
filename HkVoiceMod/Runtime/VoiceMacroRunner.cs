using System;
using System.Collections.Generic;
using HkVoiceMod.Commands;
using HkVoiceMod.Input;

namespace HkVoiceMod.Runtime
{
    public sealed class VoiceMacroRunner
    {
        private readonly List<ScheduledMacroStep> _scheduledSteps = new List<ScheduledMacroStep>();
        private VoiceModSettings _settings = new VoiceModSettings();

        public void ApplySettings(VoiceModSettings settings)
        {
            _settings = settings?.Clone() ?? new VoiceModSettings();
            CancelPendingSteps();
        }

        public void QueueMacro(VoiceMacroConfig macro, float startTime)
        {
            if (macro == null)
            {
                throw new ArgumentNullException(nameof(macro));
            }

            var scheduledTime = startTime;
            foreach (var step in macro.Steps)
            {
                if (step == null)
                {
                    continue;
                }

                if (step.StepKind == VoiceMacroStepKind.Delay)
                {
                    scheduledTime += step.DelaySeconds;
                    continue;
                }

                _scheduledSteps.Add(new ScheduledMacroStep(macro.Id, scheduledTime, step.Clone()));
            }
        }

        public void Tick(float realtimeSinceStartup, HeroActionInputInjector injector)
        {
            if (injector == null)
            {
                throw new ArgumentNullException(nameof(injector));
            }

            if (_scheduledSteps.Count == 0)
            {
                return;
            }

            List<int>? dueIndexes = null;
            for (var index = 0; index < _scheduledSteps.Count; index++)
            {
                if (_scheduledSteps[index].ExecuteAt > realtimeSinceStartup)
                {
                    continue;
                }

                if (dueIndexes == null)
                {
                    dueIndexes = new List<int>();
                }

                dueIndexes.Add(index);
            }

            if (dueIndexes == null)
            {
                return;
            }

            foreach (var index in dueIndexes)
            {
                var scheduledStep = _scheduledSteps[index];
                var step = scheduledStep.Step;
                if (step.StepKind != VoiceMacroStepKind.Action)
                {
                    continue;
                }

                injector.DispatchProfile(
                    new KeyActionProfile(step.PressMode, step.Keys, step.DurationSeconds, step.ReleaseOppositeHorizontalHold),
                    realtimeSinceStartup);
            }

            for (var index = dueIndexes.Count - 1; index >= 0; index--)
            {
                _scheduledSteps.RemoveAt(dueIndexes[index]);
            }
        }

        public void CancelPendingSteps()
        {
            _scheduledSteps.Clear();
        }

        private sealed class ScheduledMacroStep
        {
            public ScheduledMacroStep(string macroId, float executeAt, VoiceMacroStep step)
            {
                MacroId = macroId;
                ExecuteAt = executeAt;
                Step = step;
            }

            public string MacroId { get; }

            public float ExecuteAt { get; }

            public VoiceMacroStep Step { get; }
        }
    }
}
