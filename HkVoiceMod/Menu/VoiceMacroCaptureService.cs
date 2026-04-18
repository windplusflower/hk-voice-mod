using System;
using System.Collections.Generic;
using HkVoiceMod.Commands;
using UnityEngine;

namespace HkVoiceMod.Menu
{
    internal sealed class VoiceMacroCaptureService
    {
        private static readonly KeyCode[] AllKeyCodes = (KeyCode[])Enum.GetValues(typeof(KeyCode));
        private static VoiceMacroCaptureService? _instance;

        private readonly GameKeybindNameResolver _resolver = new GameKeybindNameResolver();
        private VoiceMacroCaptureBehaviour? _behaviour;
        private string? _capturingMacroId;
        private Action<HeroActionKey>? _onActionKey;
        private Action? _onDeleteLast;
        private Action? _onCancel;
        private Action? _onConfirm;

        public static VoiceMacroCaptureService Instance => _instance ?? (_instance = new VoiceMacroCaptureService());

        public GameKeybindNameResolver Resolver => _resolver;

        public bool IsCapturing(string macroId)
        {
            return string.Equals(_capturingMacroId, macroId, StringComparison.Ordinal);
        }

        public string GetStatusText(string macroId)
        {
            return IsCapturing(macroId)
                ? "录制中：按游戏当前绑定键追加步骤；Backspace 删除末尾，Esc 取消本次录制，Enter 确认本次录制。"
                : "未录制：点击“开始录制”，再按游戏当前绑定键追加步骤。";
        }

        public void BeginCapture(string macroId, Action<HeroActionKey> onActionKey, Action onDeleteLast, Action onCancel, Action onConfirm)
        {
            if (string.IsNullOrWhiteSpace(macroId))
            {
                throw new ArgumentException("Macro id is required.", nameof(macroId));
            }

            EnsureMonitor();
            _capturingMacroId = macroId;
            _onActionKey = onActionKey ?? throw new ArgumentNullException(nameof(onActionKey));
            _onDeleteLast = onDeleteLast ?? throw new ArgumentNullException(nameof(onDeleteLast));
            _onCancel = onCancel ?? throw new ArgumentNullException(nameof(onCancel));
            _onConfirm = onConfirm ?? throw new ArgumentNullException(nameof(onConfirm));
        }

        public void StopCapture()
        {
            _capturingMacroId = null;
            _onActionKey = null;
            _onDeleteLast = null;
            _onCancel = null;
            _onConfirm = null;
        }

        private void EnsureMonitor()
        {
            if (_behaviour != null)
            {
                return;
            }

            var gameObject = new GameObject("HkVoiceMod.MacroCapture");
            UnityEngine.Object.DontDestroyOnLoad(gameObject);
            _behaviour = gameObject.AddComponent<VoiceMacroCaptureBehaviour>();
            _behaviour.Initialize(this);
        }

        private void Poll()
        {
            if (_capturingMacroId == null || !global::UnityEngine.Input.anyKeyDown)
            {
                return;
            }

            foreach (var keyCode in AllKeyCodes)
            {
                if (!global::UnityEngine.Input.GetKeyDown(keyCode))
                {
                    continue;
                }

                HandleKeyDown(keyCode);
                break;
            }
        }

        private void HandleKeyDown(KeyCode keyCode)
        {
            if (keyCode == KeyCode.Backspace)
            {
                _onDeleteLast?.Invoke();
                return;
            }

            if (keyCode == KeyCode.Escape)
            {
                _onCancel?.Invoke();
                StopCapture();
                return;
            }

            if (keyCode == KeyCode.Return || keyCode == KeyCode.KeypadEnter)
            {
                _onConfirm?.Invoke();
                StopCapture();
                return;
            }

            if (_resolver.TryResolveFromCurrentBindings(keyCode, out var heroActionKey, out _))
            {
                _onActionKey?.Invoke(heroActionKey);
            }
        }

        private sealed class VoiceMacroCaptureBehaviour : MonoBehaviour
        {
            private VoiceMacroCaptureService? _service;

            public void Initialize(VoiceMacroCaptureService service)
            {
                _service = service;
            }

            private void Update()
            {
                _service?.Poll();
            }
        }
    }
}
