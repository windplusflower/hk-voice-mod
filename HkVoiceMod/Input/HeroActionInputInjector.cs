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
                    if (releaseOppositeHorizontalHold || HeroActionButtonCatalog.IsVertical(actionButton))
                    {
                        ReleaseOppositeDirectionalState(actionButton);
                    }

                    _continuousHeldButtons.Add(actionButton);
                    _scheduledReleaseTimes.Remove(actionButton);
                }

                return;
            }

            foreach (var actionButton in actionButtons)
            {
                if (HeroActionButtonCatalog.IsVertical(actionButton))
                {
                    ReleaseOppositeDirectionalState(actionButton);
                }

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
            ReleaseOppositeDirectionalState(actionButton);
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
            var hardwarePressedStates = new Dictionary<global::GlobalEnums.HeroActionButton, bool>(HeroActionButtonCatalog.SupportedGameplayButtons.Count);

            foreach (var actionButton in HeroActionButtonCatalog.SupportedGameplayButtons)
            {
                var action = ResolvePlayerAction(inputHandler, actionButton);
                if (action == null)
                {
                    continue;
                }

                var hardwarePressed = ReadHardwarePressed(action);
                hardwarePressedStates[actionButton] = hardwarePressed;
                CommitAction(action, hardwarePressed || IsActionButtonPressed(actionButton), tick, unscaledDeltaTime);
            }

            SyncMoveVector(inputActions, hardwarePressedStates, tick, unscaledDeltaTime);
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

        private static ulong GetCurrentInputManagerTick()
        {
            return global::InControl.InputManager.CurrentTick;
        }

        private void SyncMoveVector(global::HeroActions inputActions, IReadOnlyDictionary<global::GlobalEnums.HeroActionButton, bool> hardwarePressedStates, ulong tick, float unscaledDeltaTime)
        {
            var moveVector = inputActions.moveVector;
            if (moveVector == null)
            {
                return;
            }

            var horizontal = ComposeDirectionalAxis(
                ReadCachedHardwarePressed(hardwarePressedStates, global::GlobalEnums.HeroActionButton.LEFT),
                ReadCachedHardwarePressed(hardwarePressedStates, global::GlobalEnums.HeroActionButton.RIGHT),
                IsActionButtonPressed(global::GlobalEnums.HeroActionButton.LEFT),
                IsActionButtonPressed(global::GlobalEnums.HeroActionButton.RIGHT));

            var vertical = ComposeDirectionalAxis(
                ReadCachedHardwarePressed(hardwarePressedStates, global::GlobalEnums.HeroActionButton.DOWN),
                ReadCachedHardwarePressed(hardwarePressedStates, global::GlobalEnums.HeroActionButton.UP),
                IsActionButtonPressed(global::GlobalEnums.HeroActionButton.DOWN),
                IsActionButtonPressed(global::GlobalEnums.HeroActionButton.UP));

            var updateWithAxes = ResolveUpdateWithAxesMethod(moveVector.GetType());
            updateWithAxes?.Invoke(moveVector, new object[] { horizontal, vertical, tick, unscaledDeltaTime });
        }

        private static float ComposeDirectionalAxis(bool hardwareNegative, bool hardwarePositive, bool voiceNegative, bool voicePositive)
        {
            var negative = hardwareNegative || voiceNegative;
            var positive = hardwarePositive || voicePositive;

            if (negative == positive)
            {
                return 0f;
            }

            return negative ? -1f : 1f;
        }

        private static bool ReadCachedHardwarePressed(IReadOnlyDictionary<global::GlobalEnums.HeroActionButton, bool> hardwarePressedStates, global::GlobalEnums.HeroActionButton actionButton)
        {
            return hardwarePressedStates.TryGetValue(actionButton, out var isPressed) && isPressed;
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

        private void ReleaseOppositeDirectionalState(global::GlobalEnums.HeroActionButton actionButton)
        {
            var oppositeActionButton = GetOppositeDirectionalButton(actionButton);
            if (!oppositeActionButton.HasValue)
            {
                return;
            }

            _continuousHeldButtons.Remove(oppositeActionButton.Value);
            _macroHeldButtons.Remove(oppositeActionButton.Value);
            _scheduledReleaseTimes.Remove(oppositeActionButton.Value);
        }

        private static global::GlobalEnums.HeroActionButton? GetOppositeDirectionalButton(global::GlobalEnums.HeroActionButton actionButton)
        {
            switch (actionButton)
            {
                case global::GlobalEnums.HeroActionButton.LEFT:
                    return global::GlobalEnums.HeroActionButton.RIGHT;
                case global::GlobalEnums.HeroActionButton.RIGHT:
                    return global::GlobalEnums.HeroActionButton.LEFT;
                case global::GlobalEnums.HeroActionButton.DOWN:
                    return global::GlobalEnums.HeroActionButton.UP;
                case global::GlobalEnums.HeroActionButton.UP:
                    return global::GlobalEnums.HeroActionButton.DOWN;
                default:
                    return null;
            }
        }
    }
}
