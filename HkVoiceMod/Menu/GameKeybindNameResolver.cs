using System;
using InControl;
using HkVoiceMod.Commands;
using UnityEngine;

namespace HkVoiceMod.Menu
{
    internal sealed class GameKeybindNameResolver
    {
        private static readonly HeroActionKey[] SupportedKeys =
        {
            HeroActionKey.Left,
            HeroActionKey.Right,
            HeroActionKey.Up,
            HeroActionKey.Down,
            HeroActionKey.Attack,
            HeroActionKey.Jump,
            HeroActionKey.Dash,
            HeroActionKey.Cast
        };

        public bool TryResolveFromCurrentBindings(KeyCode keyCode, out HeroActionKey heroActionKey, out string displayName)
        {
            heroActionKey = default(HeroActionKey);
            displayName = string.Empty;

            var inputActions = global::InputHandler.Instance?.inputActions;
            if (inputActions == null)
            {
                return false;
            }

            foreach (var supportedKey in SupportedKeys)
            {
                var action = GetPlayerAction(inputActions, supportedKey);
                if (action == null || !ActionHasKeyBinding(action, keyCode))
                {
                    continue;
                }

                heroActionKey = supportedKey;
                displayName = GetDisplayName(supportedKey);
                return true;
            }

            return false;
        }

        public string GetDisplayName(HeroActionKey heroActionKey)
        {
            var inputActions = global::InputHandler.Instance?.inputActions;
            var action = inputActions != null ? GetPlayerAction(inputActions, heroActionKey) : null;
            var actionName = action != null ? action.Name : string.Empty;
            if (!string.IsNullOrWhiteSpace(actionName))
            {
                return actionName;
            }

            switch (heroActionKey)
            {
                case HeroActionKey.Left:
                    return "左移";
                case HeroActionKey.Right:
                    return "右移";
                case HeroActionKey.Up:
                    return "上移";
                case HeroActionKey.Down:
                    return "下移";
                case HeroActionKey.Attack:
                    return "攻击";
                case HeroActionKey.Jump:
                    return "跳跃";
                case HeroActionKey.Dash:
                    return "冲刺";
                case HeroActionKey.Cast:
                    return "法术";
                default:
                    return heroActionKey.ToString();
            }
        }

        private static bool ActionHasKeyBinding(PlayerAction action, KeyCode keyCode)
        {
            foreach (BindingSource binding in action.Bindings)
            {
                if (!TryGetBoundKeyCode(binding, out var boundKeyCode))
                {
                    continue;
                }

                if (boundKeyCode == keyCode)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetBoundKeyCode(BindingSource binding, out KeyCode keyCode)
        {
            keyCode = KeyCode.None;
            var keyBindingSource = binding as KeyBindingSource;
            if (keyBindingSource == null || keyBindingSource.Control.IncludeCount != 1)
            {
                return false;
            }

            return TryMapInControlKey(keyBindingSource.Control.GetInclude(0), out keyCode);
        }

        private static bool TryMapInControlKey(Key key, out KeyCode keyCode)
        {
            keyCode = KeyCode.None;
            var keyName = key.ToString();
            if (Enum.TryParse(keyName, out KeyCode parsedKeyCode))
            {
                keyCode = parsedKeyCode;
                return true;
            }

            switch (key)
            {
                case Key.Return:
                    keyCode = KeyCode.Return;
                    return true;
                case Key.Escape:
                    keyCode = KeyCode.Escape;
                    return true;
                case Key.Backspace:
                    keyCode = KeyCode.Backspace;
                    return true;
                case Key.LeftBracket:
                    keyCode = KeyCode.LeftBracket;
                    return true;
                case Key.RightBracket:
                    keyCode = KeyCode.RightBracket;
                    return true;
                default:
                    return false;
            }
        }

        private static PlayerAction? GetPlayerAction(global::HeroActions inputActions, HeroActionKey heroActionKey)
        {
            switch (heroActionKey)
            {
                case HeroActionKey.Left:
                    return inputActions.left;
                case HeroActionKey.Right:
                    return inputActions.right;
                case HeroActionKey.Up:
                    return inputActions.up;
                case HeroActionKey.Down:
                    return inputActions.down;
                case HeroActionKey.Attack:
                    return inputActions.attack;
                case HeroActionKey.Jump:
                    return inputActions.jump;
                case HeroActionKey.Dash:
                    return inputActions.dash;
                case HeroActionKey.Cast:
                    return inputActions.cast;
                default:
                    return null;
            }
        }
    }
}
