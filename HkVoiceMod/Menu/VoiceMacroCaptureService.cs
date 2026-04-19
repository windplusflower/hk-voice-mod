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
        private bool _isCaptureActive;
        private bool _isCaptureSuspended;
        private bool _resumeCaptureAfterSuspend;
        private int _resumeBlockedFrame = -1;
        private Action<global::GlobalEnums.HeroActionButton>? _onActionButton;
        private Action? _onDeleteLast;
        private Action? _onCancel;
        private Action? _onConfirm;

        public static VoiceMacroCaptureService Instance => _instance ?? (_instance = new VoiceMacroCaptureService());

        public GameKeybindNameResolver Resolver => _resolver;

        public bool IsCapturing(string macroId)
        {
            return string.Equals(_capturingMacroId, macroId, StringComparison.Ordinal) && _isCaptureActive;
        }

        public bool HasCaptureSession(string macroId)
        {
            return string.Equals(_capturingMacroId, macroId, StringComparison.Ordinal);
        }

        public bool IsCaptureSuspended(string macroId)
        {
            return string.Equals(_capturingMacroId, macroId, StringComparison.Ordinal) && _isCaptureSuspended;
        }

        public string GetStatusText(string macroId)
        {
            if (HasCaptureSession(macroId) && _isCaptureSuspended)
            {
                return _resumeCaptureAfterSuspend
                    ? "录制已暂停：正在编辑 Delay 毫秒数；返回录制页后会继续录制。"
                    : "录制已暂停：正在编辑 Delay 毫秒数；返回录制页后保持停止录制。";
            }

            if (IsCapturing(macroId))
            {
                return "录制中：按游戏当前绑定键追加步骤；若要删除末尾、确认或取消，请先点击“停止录制”。";
            }

            return HasCaptureSession(macroId)
                ? "未录制：点击“开始录制”后才会追加步骤；当前可用 Backspace 删除末尾、Enter 确认、Esc 取消。"
                : "未录制：点击“开始录制”后才会追加步骤。";
        }

        public void BeginCapture(string macroId, Action<global::GlobalEnums.HeroActionButton> onActionButton, Action onDeleteLast, Action onCancel, Action onConfirm)
        {
            if (string.IsNullOrWhiteSpace(macroId))
            {
                throw new ArgumentException("Macro id is required.", nameof(macroId));
            }

            EnsureMonitor();
            _capturingMacroId = macroId;
            _isCaptureActive = false;
            _isCaptureSuspended = false;
            _resumeCaptureAfterSuspend = false;
            _resumeBlockedFrame = Time.frameCount;
            _onActionButton = onActionButton ?? throw new ArgumentNullException(nameof(onActionButton));
            _onDeleteLast = onDeleteLast ?? throw new ArgumentNullException(nameof(onDeleteLast));
            _onCancel = onCancel ?? throw new ArgumentNullException(nameof(onCancel));
            _onConfirm = onConfirm ?? throw new ArgumentNullException(nameof(onConfirm));
        }

        public void StartCapture(string macroId)
        {
            if (!HasCaptureSession(macroId))
            {
                return;
            }

            _isCaptureActive = true;
            _isCaptureSuspended = false;
            _resumeCaptureAfterSuspend = false;
            _resumeBlockedFrame = Time.frameCount;
        }

        public void StopActiveCapture(string macroId)
        {
            if (!HasCaptureSession(macroId))
            {
                return;
            }

            _isCaptureActive = false;
            _isCaptureSuspended = false;
            _resumeCaptureAfterSuspend = false;
            _resumeBlockedFrame = -1;
        }

        public void SuspendCapture()
        {
            if (_capturingMacroId == null)
            {
                return;
            }

            _resumeCaptureAfterSuspend = _isCaptureActive;
            _isCaptureActive = false;
            _isCaptureSuspended = true;
        }

        public void ResumeCapture()
        {
            if (_capturingMacroId == null)
            {
                return;
            }

            _isCaptureSuspended = false;
            _isCaptureActive = _resumeCaptureAfterSuspend;
            _resumeCaptureAfterSuspend = false;
            _resumeBlockedFrame = Time.frameCount;
        }

        public void StopCapture()
        {
            _capturingMacroId = null;
            _isCaptureActive = false;
            _isCaptureSuspended = false;
            _resumeCaptureAfterSuspend = false;
            _resumeBlockedFrame = -1;
            _onActionButton = null;
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
            if (_capturingMacroId == null || _isCaptureSuspended || !global::UnityEngine.Input.anyKeyDown)
            {
                return;
            }

            if (_resumeBlockedFrame == Time.frameCount)
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
            if (!_isCaptureActive)
            {
                if (keyCode == KeyCode.Backspace)
                {
                    _onDeleteLast?.Invoke();
                }
                else if (keyCode == KeyCode.Escape)
                {
                    _onCancel?.Invoke();
                }
                else if (keyCode == KeyCode.Return || keyCode == KeyCode.KeypadEnter)
                {
                    _onConfirm?.Invoke();
                }

                return;
            }

            if (_resolver.TryResolveFromCurrentBindings(keyCode, out var actionButton, out _))
            {
                _onActionButton?.Invoke(actionButton);
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
