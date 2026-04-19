using System;
using System.Collections.Generic;
using HkVoiceMod.Commands;
using HkVoiceMod.Input;

namespace HkVoiceMod.Runtime
{
    public sealed class VoiceMacroRunner
    {
        private const float MinimumReleaseDelaySeconds = 0.0001f;

        private readonly List<ScheduledMacroEvent> _scheduledEvents = new List<ScheduledMacroEvent>();

        public void ApplySettings(VoiceModSettings settings)
        {
            CancelPendingSteps();
        }

        public void QueueMacro(VoiceMacroConfig macro, float startTime)
        {
            if (macro == null)
            {
                throw new ArgumentNullException(nameof(macro));
            }

            var scheduledTime = startTime;
            foreach (var keyEvent in macro.KeyEvents)
            {
                if (keyEvent == null)
                {
                    continue;
                }

                scheduledTime += Math.Max(0, keyEvent.DelayBeforeMilliseconds) / 1000f;
                _scheduledEvents.Add(new ScheduledMacroEvent(macro.Id, scheduledTime, keyEvent.Clone()));
            }
        }

        public void Tick(float realtimeSinceStartup, HeroActionInputInjector injector)
        {
            if (injector == null)
            {
                throw new ArgumentNullException(nameof(injector));
            }

            if (_scheduledEvents.Count == 0)
            {
                return;
            }

            List<int>? dueIndexes = null;
            for (var index = 0; index < _scheduledEvents.Count; index++)
            {
                if (_scheduledEvents[index].ExecuteAt > realtimeSinceStartup)
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

            var pressedThisTick = new HashSet<global::GlobalEnums.HeroActionButton>();
            var processedIndexes = new List<int>(dueIndexes.Count);
            foreach (var index in dueIndexes)
            {
                var scheduledEvent = _scheduledEvents[index];
                var keyEvent = scheduledEvent.Event;
                if (keyEvent.EventKind == VoiceMacroKeyEventKind.Up && pressedThisTick.Contains(keyEvent.ActionButton))
                {
                    scheduledEvent.ExecuteAt = realtimeSinceStartup + MinimumReleaseDelaySeconds;
                    continue;
                }

                if (keyEvent.EventKind == VoiceMacroKeyEventKind.Down)
                {
                    injector.PressMacroActionButton(keyEvent.ActionButton);
                    pressedThisTick.Add(keyEvent.ActionButton);
                }
                else
                {
                    injector.ReleaseMacroActionButton(keyEvent.ActionButton);
                }

                processedIndexes.Add(index);
            }

            for (var index = processedIndexes.Count - 1; index >= 0; index--)
            {
                _scheduledEvents.RemoveAt(processedIndexes[index]);
            }
        }

        public void CancelPendingSteps()
        {
            _scheduledEvents.Clear();
        }

        private sealed class ScheduledMacroEvent
        {
            public ScheduledMacroEvent(string macroId, float executeAt, VoiceMacroKeyEvent keyEvent)
            {
                MacroId = macroId;
                ExecuteAt = executeAt;
                Event = keyEvent;
            }

            public string MacroId { get; }

            public float ExecuteAt { get; set; }

            public VoiceMacroKeyEvent Event { get; }
        }
    }
}
