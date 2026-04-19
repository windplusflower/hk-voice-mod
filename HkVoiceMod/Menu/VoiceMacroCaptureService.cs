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
        private bool _isAwaitingSingleKeyInput;
        private int _resumeBlockedFrame = -1;
        private readonly Dictionary<KeyCode, ActiveCapturedKeyState> _activeCapturedKeysByKeyCode = new Dictionary<KeyCode, ActiveCapturedKeyState>();
        private bool _hasRecordedAnyEvent;
        private float _lastRecordedEventRealtime;
        private Action<CapturedMacroKeyEvent>? _onKeyEvent;
        private Action? _onDeleteLast;
        private Action? _onCancel;
        private Action? _onConfirm;
        private Action<global::GlobalEnums.HeroActionButton>? _onSingleKeyActionButton;
        private Action? _onSingleKeyCancel;

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

        public bool IsAwaitingSingleKeyInput(string macroId)
        {
            return string.Equals(_capturingMacroId, macroId, StringComparison.Ordinal) && _isAwaitingSingleKeyInput;
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
                return "录制中：会记录按下、松开与事件间隔；若要删除末尾、确认或取消，请先点击“停止录制”。";
            }

            return HasCaptureSession(macroId)
                ? "未录制：点击“开始录制”后才会追加事件；当前可用 Backspace 删除末尾、Enter 确认、Esc 取消。"
                : "未录制：点击“开始录制”后才会追加事件。";
        }

        public void BeginCapture(string macroId, Action<CapturedMacroKeyEvent> onKeyEvent, Action onDeleteLast, Action onCancel, Action onConfirm)
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
            ClearSingleKeyCapture();
            _activeCapturedKeysByKeyCode.Clear();
            _hasRecordedAnyEvent = false;
            _lastRecordedEventRealtime = 0f;
            _resumeBlockedFrame = Time.frameCount;
            _onKeyEvent = onKeyEvent ?? throw new ArgumentNullException(nameof(onKeyEvent));
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
            ClearSingleKeyCapture();
            _activeCapturedKeysByKeyCode.Clear();
            if (_hasRecordedAnyEvent)
            {
                _lastRecordedEventRealtime = Time.realtimeSinceStartup;
            }
            _resumeBlockedFrame = Time.frameCount;
        }

        public void StopActiveCapture(string macroId)
        {
            if (!HasCaptureSession(macroId))
            {
                return;
            }

            FlushActiveKeyUps();
            _isCaptureActive = false;
            _isCaptureSuspended = false;
            _resumeCaptureAfterSuspend = false;
            ClearSingleKeyCapture();
            _activeCapturedKeysByKeyCode.Clear();
            _resumeBlockedFrame = -1;
        }

        public void BeginSingleKeyCapture(string macroId, Action<global::GlobalEnums.HeroActionButton> onActionButton, Action onCancel)
        {
            if (!HasCaptureSession(macroId) || _isCaptureActive || _isCaptureSuspended)
            {
                return;
            }

            _isAwaitingSingleKeyInput = true;
            _onSingleKeyActionButton = onActionButton ?? throw new ArgumentNullException(nameof(onActionButton));
            _onSingleKeyCancel = onCancel ?? throw new ArgumentNullException(nameof(onCancel));
            _resumeBlockedFrame = Time.frameCount;
        }

        public void CancelSingleKeyCapture(string macroId)
        {
            if (!IsAwaitingSingleKeyInput(macroId))
            {
                return;
            }

            ClearSingleKeyCapture();
        }

        public void SuspendCapture()
        {
            if (_capturingMacroId == null)
            {
                return;
            }

            FlushActiveKeyUps();
            _resumeCaptureAfterSuspend = _isCaptureActive;
            _isCaptureActive = false;
            _isCaptureSuspended = true;
            _activeCapturedKeysByKeyCode.Clear();
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
            _activeCapturedKeysByKeyCode.Clear();
            if (_hasRecordedAnyEvent)
            {
                _lastRecordedEventRealtime = Time.realtimeSinceStartup;
            }
            _resumeBlockedFrame = Time.frameCount;
        }

        public void StopCapture()
        {
            FlushActiveKeyUps();
            _capturingMacroId = null;
            _isCaptureActive = false;
            _isCaptureSuspended = false;
            _resumeCaptureAfterSuspend = false;
            ClearSingleKeyCapture();
            _activeCapturedKeysByKeyCode.Clear();
            _hasRecordedAnyEvent = false;
            _lastRecordedEventRealtime = 0f;
            _resumeBlockedFrame = -1;
            _onKeyEvent = null;
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
            if (_capturingMacroId == null || _isCaptureSuspended)
            {
                return;
            }

            if (_resumeBlockedFrame == Time.frameCount)
            {
                return;
            }

            foreach (var keyCode in AllKeyCodes)
            {
                if (global::UnityEngine.Input.GetKeyDown(keyCode))
                {
                    HandleKeyDown(keyCode);
                }

                if (_isCaptureActive && global::UnityEngine.Input.GetKeyUp(keyCode))
                {
                    HandleKeyUp(keyCode);
                }
            }
        }

        private void HandleKeyDown(KeyCode keyCode)
        {
            if (_isAwaitingSingleKeyInput)
            {
                if (keyCode == KeyCode.Escape)
                {
                    var onCancel = _onSingleKeyCancel;
                    ClearSingleKeyCapture();
                    onCancel?.Invoke();
                }
                else if (_resolver.TryResolveFromCurrentBindings(keyCode, out var resolvedActionButton, out _))
                {
                    var onSingleKeyActionButton = _onSingleKeyActionButton;
                    ClearSingleKeyCapture();
                    onSingleKeyActionButton?.Invoke(resolvedActionButton);
                }

                return;
            }

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

            if (_activeCapturedKeysByKeyCode.ContainsKey(keyCode))
            {
                return;
            }

            if (_resolver.TryResolveFromCurrentBindings(keyCode, out var actionButton, out _))
            {
                if (ContainsActiveActionButton(actionButton))
                {
                    return;
                }

                var pairId = Guid.NewGuid().ToString("N");
                _activeCapturedKeysByKeyCode[keyCode] = new ActiveCapturedKeyState(actionButton, pairId);
                RecordKeyEvent(actionButton, VoiceMacroKeyEventKind.Down, pairId);
            }
        }

        private void HandleKeyUp(KeyCode keyCode)
        {
            if (!_activeCapturedKeysByKeyCode.TryGetValue(keyCode, out var activeKeyState))
            {
                return;
            }

            _activeCapturedKeysByKeyCode.Remove(keyCode);
            RecordKeyEvent(activeKeyState.ActionButton, VoiceMacroKeyEventKind.Up, activeKeyState.PairId);
        }

        private void RecordKeyEvent(global::GlobalEnums.HeroActionButton actionButton, VoiceMacroKeyEventKind eventKind, string pairId)
        {
            var now = Time.realtimeSinceStartup;
            var delayBeforeMilliseconds = 0;
            if (_hasRecordedAnyEvent)
            {
                delayBeforeMilliseconds = Math.Max(0, (int)Math.Round((now - _lastRecordedEventRealtime) * 1000f, MidpointRounding.AwayFromZero));
            }

            _lastRecordedEventRealtime = now;
            _hasRecordedAnyEvent = true;
            _onKeyEvent?.Invoke(new CapturedMacroKeyEvent(actionButton, eventKind, delayBeforeMilliseconds, pairId));
        }

        private void ClearSingleKeyCapture()
        {
            _isAwaitingSingleKeyInput = false;
            _onSingleKeyActionButton = null;
            _onSingleKeyCancel = null;
        }

        private bool ContainsActiveActionButton(global::GlobalEnums.HeroActionButton actionButton)
        {
            foreach (var activeKeyState in _activeCapturedKeysByKeyCode.Values)
            {
                if (activeKeyState.ActionButton == actionButton)
                {
                    return true;
                }
            }

            return false;
        }

        private void FlushActiveKeyUps()
        {
            if (!_isCaptureActive || _activeCapturedKeysByKeyCode.Count == 0)
            {
                return;
            }

            var activeKeyStates = new List<ActiveCapturedKeyState>(_activeCapturedKeysByKeyCode.Values);
            for (var index = 0; index < activeKeyStates.Count; index++)
            {
                RecordKeyEvent(activeKeyStates[index].ActionButton, VoiceMacroKeyEventKind.Up, activeKeyStates[index].PairId);
            }
        }

        private readonly struct ActiveCapturedKeyState
        {
            public ActiveCapturedKeyState(global::GlobalEnums.HeroActionButton actionButton, string pairId)
            {
                ActionButton = actionButton;
                PairId = pairId ?? string.Empty;
            }

            public global::GlobalEnums.HeroActionButton ActionButton { get; }

            public string PairId { get; }
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
