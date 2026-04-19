using System;
using System.Collections.Generic;
using System.Reflection;
using HkVoiceMod.Commands;
using InControl;

namespace HkVoiceMod.Input
{
    public sealed class HeroActionInputInjector : IInputInjector
    {
        private readonly Dictionary<global::GlobalEnums.HeroActionButton, float> _scheduledReleaseTimes = new Dictionary<global::GlobalEnums.HeroActionButton, float>();
        private readonly HashSet<global::GlobalEnums.HeroActionButton> _continuousHeldButtons = new HashSet<global::GlobalEnums.HeroActionButton>();
        private readonly HashSet<global::GlobalEnums.HeroActionButton> _macroHeldButtons = new HashSet<global::GlobalEnums.HeroActionButton>();

        private VoiceModSettings _settings;
        private MethodInfo? _updateWithAxesMethod;

        public HeroActionInputInjector(VoiceModSettings settings)
        {
            _settings = settings?.Clone() ?? new VoiceModSettings();
        }

        public void ApplySettings(VoiceModSettings settings)
        {
            _settings = settings?.Clone() ?? new VoiceModSettings();
        }

        public void Dispatch(VoiceCommand command, float realtimeSinceStartup)
        {
            var profile = VoiceCommandMap.GetProfile(command, _settings);
            DispatchProfile(profile, realtimeSinceStartup);
        }

        public void DispatchProfile(KeyActionProfile profile, float realtimeSinceStartup)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            DispatchActionButtons(HeroActionButtonCatalog.MapLegacyKeys(profile.Keys), profile.Mode, profile.DurationSeconds, profile.ReleaseOppositeHorizontalHold, realtimeSinceStartup);
        }

        public void DispatchMacroActions(IReadOnlyList<global::GlobalEnums.HeroActionButton> actionButtons, KeyPressMode mode, float durationSeconds, bool releaseOppositeHorizontalHold, float realtimeSinceStartup)
        {
            DispatchActionButtons(actionButtons, mode, durationSeconds, releaseOppositeHorizontalHold, realtimeSinceStartup);
        }

        private void DispatchActionButtons(IReadOnlyList<global::GlobalEnums.HeroActionButton> actionButtons, KeyPressMode mode, float durationSeconds, bool releaseOppositeHorizontalHold, float realtimeSinceStartup)
        {
            if (actionButtons == null)
            {
                throw new ArgumentNullException(nameof(actionButtons));
            }

            if (mode == KeyPressMode.ReleaseContinuous)
            {
                ReleaseContinuousInputs();
                return;
            }

            if (mode == KeyPressMode.ContinuousHold)
            {
                foreach (var actionButton in actionButtons)
                {
                    if (releaseOppositeHorizontalHold)
                    {
                        ReleaseOppositeHorizontalHold(actionButton);
                    }

                    _continuousHeldButtons.Add(actionButton);
                    _scheduledReleaseTimes.Remove(actionButton);
                }

                return;
            }

            foreach (var actionButton in actionButtons)
            {
                var releaseAt = realtimeSinceStartup + durationSeconds;
                if (_scheduledReleaseTimes.TryGetValue(actionButton, out var existingReleaseAt) && existingReleaseAt > releaseAt)
                {
                    continue;
                }

                _scheduledReleaseTimes[actionButton] = releaseAt;
            }
        }

        public void Tick(float unscaledDeltaTime, float realtimeSinceStartup)
        {
            ReleaseExpiredTimedKeys(realtimeSinceStartup);
            ApplyCurrentInputState(unscaledDeltaTime, realtimeSinceStartup);
        }

        public void ReleaseContinuousInputs()
        {
            _continuousHeldButtons.Remove(global::GlobalEnums.HeroActionButton.LEFT);
            _continuousHeldButtons.Remove(global::GlobalEnums.HeroActionButton.RIGHT);
        }

        public void PressMacroActionButton(global::GlobalEnums.HeroActionButton actionButton)
        {
            _macroHeldButtons.Add(actionButton);
        }

        public void ReleaseMacroActionButton(global::GlobalEnums.HeroActionButton actionButton)
        {
            _macroHeldButtons.Remove(actionButton);
        }

        public void ReleaseAllMacroActionButtons()
        {
            _macroHeldButtons.Clear();
        }

        public void ResetAllInputs(float realtimeSinceStartup)
        {
            _continuousHeldButtons.Clear();
            _scheduledReleaseTimes.Clear();
            _macroHeldButtons.Clear();
            ApplyCurrentInputState(0f, realtimeSinceStartup);
        }

        private void ReleaseExpiredTimedKeys(float realtimeSinceStartup)
        {
            if (_scheduledReleaseTimes.Count == 0)
            {
                return;
            }

            List<global::GlobalEnums.HeroActionButton>? expiredKeys = null;
            foreach (var entry in _scheduledReleaseTimes)
            {
                if (entry.Value > realtimeSinceStartup)
                {
                    continue;
                }

                if (expiredKeys == null)
                {
                    expiredKeys = new List<global::GlobalEnums.HeroActionButton>();
                }

                expiredKeys.Add(entry.Key);
            }

            if (expiredKeys == null)
            {
                return;
            }

            foreach (var key in expiredKeys)
            {
                _scheduledReleaseTimes.Remove(key);
            }
        }

        private void ApplyCurrentInputState(float unscaledDeltaTime, float realtimeSinceStartup)
        {
            var inputHandler = global::InputHandler.Instance;
            if (inputHandler == null)
            {
                return;
            }

            var inputActions = inputHandler.inputActions;
            if (inputActions == null)
            {
                return;
            }

            var tick = GetCurrentInputManagerTick();

            var moveVector = inputActions.moveVector;
            var hardwareHorizontal = moveVector != null ? ReadHardwareAxis(moveVector.X) : 0f;
            var hardwareVertical = moveVector != null ? ReadHardwareAxis(moveVector.Y) : 0f;

            foreach (var actionButton in HeroActionButtonCatalog.SupportedGameplayButtons)
            {
                var action = ResolvePlayerAction(inputHandler, actionButton);
                if (action == null)
                {
                    continue;
                }

                CommitAction(action, ReadHardwarePressed(action) || IsActionButtonPressed(actionButton), tick, unscaledDeltaTime);
            }

            SyncMoveVector(inputActions, hardwareHorizontal, hardwareVertical, tick, unscaledDeltaTime);
        }

        private bool IsActionButtonPressed(global::GlobalEnums.HeroActionButton actionButton)
        {
            return _macroHeldButtons.Contains(actionButton)
                || _continuousHeldButtons.Contains(actionButton)
                || _scheduledReleaseTimes.ContainsKey(actionButton);
        }

        private static void CommitAction(PlayerAction action, bool state, ulong tick, float unscaledDeltaTime)
        {
            action?.CommitWithState(state, tick, unscaledDeltaTime);
        }

        private static bool ReadHardwarePressed(PlayerAction? action)
        {
            return action != null && action.IsPressed;
        }

        private static float ReadHardwareAxis(float axis)
        {
            if (float.IsNaN(axis) || float.IsInfinity(axis))
            {
                return 0f;
            }

            return axis;
        }

        private static ulong GetCurrentInputManagerTick()
        {
            return global::InControl.InputManager.CurrentTick;
        }

        private void SyncMoveVector(global::HeroActions inputActions, float hardwareHorizontal, float hardwareVertical, ulong tick, float unscaledDeltaTime)
        {
            var moveVector = inputActions.moveVector;
            if (moveVector == null)
            {
                return;
            }

            var voiceHorizontal = 0f;
            if (IsActionButtonPressed(global::GlobalEnums.HeroActionButton.LEFT))
            {
                voiceHorizontal -= 1f;
            }

            if (IsActionButtonPressed(global::GlobalEnums.HeroActionButton.RIGHT))
            {
                voiceHorizontal += 1f;
            }

            var voiceVertical = 0f;
            if (IsActionButtonPressed(global::GlobalEnums.HeroActionButton.DOWN))
            {
                voiceVertical -= 1f;
            }

            if (IsActionButtonPressed(global::GlobalEnums.HeroActionButton.UP))
            {
                voiceVertical += 1f;
            }

            var horizontal = Math.Max(-1f, Math.Min(1f, hardwareHorizontal + voiceHorizontal));
            var vertical = Math.Max(-1f, Math.Min(1f, hardwareVertical + voiceVertical));

            var updateWithAxes = ResolveUpdateWithAxesMethod(moveVector.GetType());
            updateWithAxes?.Invoke(moveVector, new object[] { horizontal, vertical, tick, unscaledDeltaTime });
        }

        private MethodInfo? ResolveUpdateWithAxesMethod(Type moveVectorType)
        {
            if (_updateWithAxesMethod != null)
            {
                return _updateWithAxesMethod;
            }

            _updateWithAxesMethod = moveVectorType.GetMethod(
                "UpdateWithAxes",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(float), typeof(float), typeof(ulong), typeof(float) },
                null);

            if (_updateWithAxesMethod == null && moveVectorType.BaseType != null)
            {
                _updateWithAxesMethod = moveVectorType.BaseType.GetMethod(
                    "UpdateWithAxes",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(float), typeof(float), typeof(ulong), typeof(float) },
                    null);
            }

            return _updateWithAxesMethod;
        }

        private static PlayerAction? ResolvePlayerAction(global::InputHandler inputHandler, global::GlobalEnums.HeroActionButton actionButton)
        {
            return inputHandler.ActionButtonToPlayerAction(actionButton);
        }

        private void ReleaseOppositeHorizontalHold(global::GlobalEnums.HeroActionButton actionButton)
        {
            if (actionButton == global::GlobalEnums.HeroActionButton.LEFT)
            {
                _continuousHeldButtons.Remove(global::GlobalEnums.HeroActionButton.RIGHT);
                return;
            }

            if (actionButton == global::GlobalEnums.HeroActionButton.RIGHT)
            {
                _continuousHeldButtons.Remove(global::GlobalEnums.HeroActionButton.LEFT);
            }
        }
    }
}
