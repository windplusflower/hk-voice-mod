using System;
using HkVoiceMod.Commands;
using InControl;
using UnityEngine;

namespace HkVoiceMod.Menu
{
    internal sealed class GameKeybindNameResolver
    {
        public bool TryResolveFromCurrentBindings(KeyCode keyCode, out global::GlobalEnums.HeroActionButton actionButton, out string displayName)
        {
            actionButton = default(global::GlobalEnums.HeroActionButton);
            displayName = string.Empty;

            foreach (var supportedButton in HeroActionButtonCatalog.SupportedGameplayButtons)
            {
                if (!TryGetPlayerAction(supportedButton, out var action) || action == null || !ActionHasKeyBinding(action, keyCode))
                {
                    continue;
                }

                actionButton = supportedButton;
                displayName = GetDisplayName(supportedButton);
                return true;
            }

            return false;
        }

        public string GetDisplayName(global::GlobalEnums.HeroActionButton actionButton)
        {
            var inputHandler = global::InputHandler.Instance;
            if (inputHandler != null && TryGetPlayerAction(actionButton, out var action) && action != null)
            {
                var localizedName = inputHandler.ActionButtonLocalizedKey(action);
                if (IsMeaningfulDisplayName(localizedName))
                {
                    return localizedName;
                }

                if (IsMeaningfulDisplayName(action.Name))
                {
                    return action.Name;
                }
            }

            switch (actionButton)
            {
                case global::GlobalEnums.HeroActionButton.LEFT:
                    return "左移";
                case global::GlobalEnums.HeroActionButton.RIGHT:
                    return "右移";
                case global::GlobalEnums.HeroActionButton.UP:
                    return "上移";
                case global::GlobalEnums.HeroActionButton.DOWN:
                    return "下移";
                case global::GlobalEnums.HeroActionButton.ATTACK:
                    return "攻击";
                case global::GlobalEnums.HeroActionButton.JUMP:
                    return "跳跃";
                case global::GlobalEnums.HeroActionButton.DASH:
                    return "冲刺";
                case global::GlobalEnums.HeroActionButton.SUPER_DASH:
                    return "超级冲刺";
                case global::GlobalEnums.HeroActionButton.CAST:
                    return "法术";
                case global::GlobalEnums.HeroActionButton.QUICK_CAST:
                    return "快速施法";
                case global::GlobalEnums.HeroActionButton.DREAM_NAIL:
                    return "梦之钉";
                case global::GlobalEnums.HeroActionButton.QUICK_MAP:
                    return "快速地图";
                case global::GlobalEnums.HeroActionButton.INVENTORY:
                    return "物品栏";
                default:
                    return actionButton.ToString();
            }
        }

        private static bool IsMeaningfulDisplayName(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value!.Trim();
            var normalized = trimmed.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
            return !normalized.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase)
                && !normalized.Equals("UNKNOWNKEY", StringComparison.OrdinalIgnoreCase)
                && !normalized.Equals("UNBOUND", StringComparison.OrdinalIgnoreCase)
                && !normalized.Equals("NONE", StringComparison.OrdinalIgnoreCase);
        }

        public bool TryGetPlayerAction(global::GlobalEnums.HeroActionButton actionButton, out PlayerAction? action)
        {
            var inputHandler = global::InputHandler.Instance;
            if (inputHandler == null)
            {
                action = null;
                return false;
            }

            action = inputHandler.ActionButtonToPlayerAction(actionButton);
            return action != null;
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

    }
}
