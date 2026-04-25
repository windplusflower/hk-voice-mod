using System;
using System.Collections.Generic;
using HkVoiceMod.Commands;
using InControl;
using UnityEngine;
using UnityEngine.UI;

namespace HkVoiceMod.Menu
{
    internal sealed class GameKeybindNameResolver
    {
        private readonly Dictionary<global::GlobalEnums.HeroActionButton, string> _keyboardMenuLabelCache = new Dictionary<global::GlobalEnums.HeroActionButton, string>();
        private int _keyboardMenuLabelCacheFrame = -1;
        private float _nextKeyboardMenuLabelScanTime;

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
            if (RequiresKeyboardMenuLabelCapture(actionButton))
            {
                if (TryGetKeyboardMenuLabel(actionButton, out var keyboardMenuLabel))
                {
                    return keyboardMenuLabel;
                }

                if (TryGetDirectionalDisplayNameFallback(actionButton, out var directionalDisplayName))
                {
                    return directionalDisplayName;
                }
            }

            var inputHandler = global::InputHandler.Instance;
            if (inputHandler != null && TryGetPlayerAction(actionButton, out var action) && action != null)
            {
                var localizedKey = inputHandler.ActionButtonLocalizedKey(action);
                if (IsMeaningfulDisplayName(localizedKey))
                {
                    var localizedName = global::Language.Language.Get(localizedKey, "MainMenu");
                    if (IsMeaningfulTranslatedDisplayName(localizedName, localizedKey))
                    {
                        return localizedName.Trim();
                    }
                }

                if (IsMeaningfulDisplayName(action.Name))
                {
                    return action.Name.Trim();
                }
            }

            switch (actionButton)
            {
                case global::GlobalEnums.HeroActionButton.LEFT:
                    return "左";
                case global::GlobalEnums.HeroActionButton.RIGHT:
                    return "右";
                case global::GlobalEnums.HeroActionButton.UP:
                    return "上";
                case global::GlobalEnums.HeroActionButton.DOWN:
                    return "下";
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

        public void PrimeKeyboardMenuLabelCache()
        {
            _keyboardMenuLabelCacheFrame = -1;
            _nextKeyboardMenuLabelScanTime = 0f;
            RefreshKeyboardMenuLabelCache();
        }

        private bool TryGetKeyboardMenuLabel(global::GlobalEnums.HeroActionButton actionButton, out string displayName)
        {
            RefreshKeyboardMenuLabelCache();
            if (_keyboardMenuLabelCache.TryGetValue(actionButton, out var cachedLabel) && IsMeaningfulDisplayName(cachedLabel))
            {
                displayName = cachedLabel;
                return true;
            }

            displayName = string.Empty;
            return false;
        }

        private void RefreshKeyboardMenuLabelCache()
        {
            if (_keyboardMenuLabelCacheFrame == Time.frameCount)
            {
                return;
            }

            if (Time.unscaledTime < _nextKeyboardMenuLabelScanTime)
            {
                return;
            }

            _keyboardMenuLabelCacheFrame = Time.frameCount;
            var mappableKeys = Resources.FindObjectsOfTypeAll<MappableKey>();
            if (mappableKeys == null || mappableKeys.Length == 0)
            {
                _nextKeyboardMenuLabelScanTime = Time.unscaledTime + 1f;
                return;
            }

            var foundActiveKeyboardMenu = false;
            var nextCache = new Dictionary<global::GlobalEnums.HeroActionButton, string>();

            foreach (var mappableKey in mappableKeys)
            {
                if (mappableKey == null
                    || !mappableKey.gameObject.scene.IsValid()
                    || !mappableKey.gameObject.activeInHierarchy
                    || !mappableKey.isActiveAndEnabled
                    || mappableKey.keymapText == null
                    || !mappableKey.keymapText.gameObject.activeInHierarchy)
                {
                    continue;
                }

                foundActiveKeyboardMenu = true;

                if (TryExtractKeyboardMenuLabel(mappableKey, out var labelText))
                {
                    nextCache[mappableKey.actionButtonType] = labelText;
                }
            }

            if (!foundActiveKeyboardMenu)
            {
                _nextKeyboardMenuLabelScanTime = Time.unscaledTime + 1f;
                return;
            }

            if (nextCache.Count > 0)
            {
                _keyboardMenuLabelCache.Clear();
                foreach (var pair in nextCache)
                {
                    _keyboardMenuLabelCache[pair.Key] = pair.Value;
                }
            }

            _nextKeyboardMenuLabelScanTime = Time.unscaledTime + 0.25f;
        }

        private static bool RequiresKeyboardMenuLabelCapture(global::GlobalEnums.HeroActionButton actionButton)
        {
            return actionButton == global::GlobalEnums.HeroActionButton.LEFT
                || actionButton == global::GlobalEnums.HeroActionButton.RIGHT
                || actionButton == global::GlobalEnums.HeroActionButton.UP
                || actionButton == global::GlobalEnums.HeroActionButton.DOWN;
        }

        private static bool TryGetDirectionalDisplayNameFallback(global::GlobalEnums.HeroActionButton actionButton, out string displayName)
        {
            switch (actionButton)
            {
                case global::GlobalEnums.HeroActionButton.LEFT:
                    displayName = "左";
                    return true;
                case global::GlobalEnums.HeroActionButton.RIGHT:
                    displayName = "右";
                    return true;
                case global::GlobalEnums.HeroActionButton.UP:
                    displayName = "上";
                    return true;
                case global::GlobalEnums.HeroActionButton.DOWN:
                    displayName = "下";
                    return true;
                default:
                    displayName = string.Empty;
                    return false;
            }
        }

        private static bool TryExtractKeyboardMenuLabel(MappableKey mappableKey, out string displayName)
        {
            Transform? scope = mappableKey.transform.parent;
            for (var depth = 0; depth < 3 && scope != null; depth++, scope = scope.parent)
            {
                if (TryFindClosestLabelText(scope, mappableKey.keymapText, out displayName))
                {
                    return true;
                }
            }

            displayName = string.Empty;
            return false;
        }

        private static bool TryFindClosestLabelText(Transform scope, Text keymapText, out string displayName)
        {
            Text? bestCandidate = null;
            var bestDistance = float.MaxValue;
            foreach (var text in scope.GetComponentsInChildren<Text>(true))
            {
                if (text == null || text == keymapText || !text.gameObject.scene.IsValid() || !text.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!IsMeaningfulMenuLabelText(text.text))
                {
                    continue;
                }

                if (text.rectTransform.position.x >= keymapText.rectTransform.position.x - 1f)
                {
                    continue;
                }

                var distance = Vector3.SqrMagnitude(text.rectTransform.position - keymapText.rectTransform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestCandidate = text;
                }
            }

            if (bestCandidate != null)
            {
                displayName = bestCandidate.text.Trim();
                return true;
            }

            displayName = string.Empty;
            return false;
        }

        private static bool IsMeaningfulMenuLabelText(string? value)
        {
            if (!IsMeaningfulDisplayName(value))
            {
                return false;
            }

            var trimmed = value!.Trim();
            return !trimmed.Equals("Keyboard", StringComparison.OrdinalIgnoreCase)
                && !trimmed.Equals("键盘", StringComparison.OrdinalIgnoreCase)
                && !trimmed.Equals("Press Key", StringComparison.OrdinalIgnoreCase)
                && !trimmed.Equals("按下按键", StringComparison.OrdinalIgnoreCase)
                && !trimmed.Equals("Unmapped", StringComparison.OrdinalIgnoreCase)
                && !trimmed.Equals("未映射", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMeaningfulTranslatedDisplayName(string? value, string rawLanguageKey)
        {
            return IsMeaningfulDisplayName(value)
                && !string.Equals(value!.Trim(), rawLanguageKey.Trim(), StringComparison.OrdinalIgnoreCase);
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
