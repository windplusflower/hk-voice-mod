using System;
using System.Collections.Generic;
using System.Reflection;
using HkVoiceMod.Commands;
using InControl;

namespace HkVoiceMod.Input
{
    public sealed class HeroActionInputInjector : IInputInjector
    {
        private readonly Dictionary<HeroActionKey, float> _scheduledReleaseTimes = new Dictionary<HeroActionKey, float>();
        private readonly HashSet<HeroActionKey> _continuousHeldKeys = new HashSet<HeroActionKey>();

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

            if (profile.Mode == KeyPressMode.ReleaseContinuous)
            {
                ReleaseContinuousInputs();
                return;
            }

            if (profile.Mode == KeyPressMode.ContinuousHold)
            {
                foreach (var key in profile.Keys)
                {
                    if (profile.ReleaseOppositeHorizontalHold)
                    {
                        ReleaseOppositeHorizontalHold(key);
                    }

                    _continuousHeldKeys.Add(key);
                    _scheduledReleaseTimes.Remove(key);
                }

                return;
            }

            foreach (var key in profile.Keys)
            {
                var releaseAt = realtimeSinceStartup + profile.DurationSeconds;
                if (_scheduledReleaseTimes.TryGetValue(key, out var existingReleaseAt) && existingReleaseAt > releaseAt)
                {
                    continue;
                }

                _scheduledReleaseTimes[key] = releaseAt;
            }
        }

        public void Tick(float unscaledDeltaTime, float realtimeSinceStartup)
        {
            ReleaseExpiredTimedKeys(realtimeSinceStartup);
            ApplyCurrentInputState(unscaledDeltaTime, realtimeSinceStartup);
        }

        public void ReleaseContinuousInputs()
        {
            _continuousHeldKeys.Remove(HeroActionKey.Left);
            _continuousHeldKeys.Remove(HeroActionKey.Right);
        }

        public void ResetAllInputs(float realtimeSinceStartup)
        {
            _continuousHeldKeys.Clear();
            _scheduledReleaseTimes.Clear();
            ApplyCurrentInputState(0f, realtimeSinceStartup);
        }

        private void ReleaseExpiredTimedKeys(float realtimeSinceStartup)
        {
            if (_scheduledReleaseTimes.Count == 0)
            {
                return;
            }

            List<HeroActionKey>? expiredKeys = null;
            foreach (var entry in _scheduledReleaseTimes)
            {
                if (entry.Value > realtimeSinceStartup)
                {
                    continue;
                }

                if (expiredKeys == null)
                {
                    expiredKeys = new List<HeroActionKey>();
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
            var inputActions = inputHandler?.inputActions;
            if (inputActions == null)
            {
                return;
            }

            var tick = GetCurrentInputManagerTick();

            var moveVector = inputActions.moveVector;
            var hardwareHorizontal = moveVector != null ? ReadHardwareAxis(moveVector.X) : 0f;
            var hardwareVertical = moveVector != null ? ReadHardwareAxis(moveVector.Y) : 0f;

            CommitAction(inputActions.left, ReadHardwarePressed(inputActions.left) || IsKeyPressed(HeroActionKey.Left), tick, unscaledDeltaTime);
            CommitAction(inputActions.right, ReadHardwarePressed(inputActions.right) || IsKeyPressed(HeroActionKey.Right), tick, unscaledDeltaTime);
            CommitAction(inputActions.up, ReadHardwarePressed(inputActions.up) || IsKeyPressed(HeroActionKey.Up), tick, unscaledDeltaTime);
            CommitAction(inputActions.down, ReadHardwarePressed(inputActions.down) || IsKeyPressed(HeroActionKey.Down), tick, unscaledDeltaTime);
            CommitAction(inputActions.attack, ReadHardwarePressed(inputActions.attack) || IsKeyPressed(HeroActionKey.Attack), tick, unscaledDeltaTime);
            CommitAction(inputActions.jump, ReadHardwarePressed(inputActions.jump) || IsKeyPressed(HeroActionKey.Jump), tick, unscaledDeltaTime);
            CommitAction(inputActions.dash, ReadHardwarePressed(inputActions.dash) || IsKeyPressed(HeroActionKey.Dash), tick, unscaledDeltaTime);
            CommitAction(inputActions.cast, ReadHardwarePressed(inputActions.cast) || IsKeyPressed(HeroActionKey.Cast), tick, unscaledDeltaTime);

            SyncMoveVector(inputActions, hardwareHorizontal, hardwareVertical, tick, unscaledDeltaTime);
        }

        private bool IsKeyPressed(HeroActionKey key)
        {
            return _continuousHeldKeys.Contains(key) || _scheduledReleaseTimes.ContainsKey(key);
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
            if (IsKeyPressed(HeroActionKey.Left))
            {
                voiceHorizontal -= 1f;
            }

            if (IsKeyPressed(HeroActionKey.Right))
            {
                voiceHorizontal += 1f;
            }

            var voiceVertical = 0f;
            if (IsKeyPressed(HeroActionKey.Down))
            {
                voiceVertical -= 1f;
            }

            if (IsKeyPressed(HeroActionKey.Up))
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

        private void ReleaseOppositeHorizontalHold(HeroActionKey key)
        {
            if (key == HeroActionKey.Left)
            {
                _continuousHeldKeys.Remove(HeroActionKey.Right);
                return;
            }

            if (key == HeroActionKey.Right)
            {
                _continuousHeldKeys.Remove(HeroActionKey.Left);
            }
        }
    }
}
