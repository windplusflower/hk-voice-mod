using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HkVoiceMod.Commands;
using HkVoiceMod.Menu;
using Modding;
using Modding.Menu;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

namespace HkVoiceMod.UI
{
    internal sealed class VoiceSettingsWindowController : MonoBehaviour
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private const float WindowWidth = 1460f;
        private const float WindowHeight = 880f;
        private const float ModalWidth = 1040f;
        private const float ModalHeight = 600f;
        private const float DelayModalWidth = 720f;
        private const float DelayModalHeight = 320f;
        private const float SectionSpacing = 8f;
        private const float OrnamentSectionHeight = 72f;
        private const float OrnamentVerticalScale = 1.35f;
        private const float RowSpacing = 10f;
        private const float FieldHeight = 50f;
        private const float StopWakeWordWidth = 360f;
        private const float StopThresholdWidth = 200f;
        private const float MacroWakeWordWidth = 460f;
        private const float MacroThresholdWidth = 180f;
        private const float PrimaryButtonWidth = 170f;
        private const float SecondaryButtonWidth = 140f;
        private const float DiscardButtonWidth = 220f;
        private const float DeleteButtonWidth = 120f;
        private const float MenuTransitionStageDelay = 0.1f;
        private const float DefaultMenuFadeSpeed = 3.2f;

        internal static readonly Color FullscreenDimColor = new Color(0.02f, 0.02f, 0.04f, 0.84f);
        internal static readonly Color WindowColor = new Color(0.07f, 0.07f, 0.09f, 0.97f);
        internal static readonly Color SectionColor = new Color(0.11f, 0.11f, 0.13f, 0.98f);
        internal static readonly Color RowColor = new Color(0.14f, 0.14f, 0.16f, 1f);
        private static readonly Color ModalColor = new Color(0.08f, 0.08f, 0.10f, 0.99f);
        internal static readonly Color InputColor = new Color(0.06f, 0.06f, 0.07f, 1f);
        internal static readonly Color PrimaryButtonColor = new Color(0.31f, 0.31f, 0.34f, 0.99f);
        internal static readonly Color SecondaryButtonColor = new Color(0.22f, 0.22f, 0.25f, 0.99f);
        internal static readonly Color DangerButtonColor = new Color(0.25f, 0.24f, 0.24f, 0.99f);
        internal static readonly Color TextColor = new Color(0.92f, 0.90f, 0.85f, 1f);
        internal static readonly Color MutedTextColor = new Color(0.71f, 0.69f, 0.64f, 1f);
        internal static readonly Color PlaceholderTextColor = new Color(0.50f, 0.49f, 0.46f, 0.94f);
        internal static readonly Color SuccessTextColor = new Color(0.81f, 0.84f, 0.81f, 1f);
        internal static readonly Color ErrorTextColor = new Color(0.82f, 0.78f, 0.76f, 1f);

        private static VoiceSettingsWindowController? _instance;

        private readonly List<MacroRowWidgets> _macroRows = new List<MacroRowWidgets>();
        private readonly List<RecordingStepRowWidgets> _recordingStepRows = new List<RecordingStepRowWidgets>();
        private readonly List<RecordingTimelineRowModel> _recordingTimelineRows = new List<RecordingTimelineRowModel>();
        private Font? _font;
        private VoiceSettingsTheme? _theme;
        private HkVoiceMod? _mod;
        private VoiceSettingsDraft? _draft;
        private Canvas? _canvas;
        private CanvasGroup? _canvasGroup;
        private CanvasGroup? _windowCanvasGroup;
        private Coroutine? _pendingRevealCoroutine;
        private Coroutine? _pendingTransitionCoroutine;
        private Sprite? _ornamentSectionSprite;
        private CanvasGroup? _windowTopOrnamentCanvasGroup;
        private CanvasGroup? _windowHeaderCanvasGroup;
        private CanvasGroup? _windowStopContentCanvasGroup;
        private CanvasGroup? _windowMacroCanvasGroup;
        private CanvasGroup? _windowFooterCanvasGroup;
        private CanvasGroup? _windowBottomOrnamentCanvasGroup;
        private RectTransform? _macroListContent;
        private GameObject? _modalHost;
        private GameObject? _recordingModal;
        private GameObject? _delayModal;
        private CanvasGroup? _recordingModalCanvasGroup;
        private CanvasGroup? _recordingModalTitleCanvasGroup;
        private CanvasGroup? _recordingModalHintCanvasGroup;
        private CanvasGroup? _recordingModalPreviewCanvasGroup;
        private CanvasGroup? _recordingModalStatusCanvasGroup;
        private CanvasGroup? _recordingModalActionsCanvasGroup;
        private RectTransform? _recordingStepListContent;
        private Text? _statusText;
        private InputField? _stopWakeWordInput;
        private InputField? _stopThresholdInput;
        private InputField? _delayInputField;
        private Text? _recordingTitleText;
        private Text? _recordingHintText;
        private Text? _recordingEmptyText;
        private Text? _recordingStatusText;
        private Text? _delayTitleText;
        private Text? _delayHintText;
        private Button? _recordingStartButton;
        private Button? _recordingStopButton;
        private Button? _recordingClearButton;
        private Button? _recordingConfirmButton;
        private Button? _recordingCancelButton;
        private VoiceMacroConfig? _recordingMacro;
        private VoiceMacroConfig? _delayMacro;
        private List<VoiceMacroKeyEvent>? _recordingStartSnapshot;
        private int _delayModalBlockedFrame = -1;
        private string? _editingPairId;
        private int _editingDelayStepIndex = -1;
        private string _stopThresholdText = string.Empty;
        private bool _isClosingWindow;
        private bool _hasLoggedRecordingTextProbe;
        private MenuScreen? _hiddenNativeMenuScreen;
        private bool _hiddenNativeMenuWasActive;

        internal static VoiceSettingsWindowController Instance
        {
            get
            {
                if (_instance != null)
                {
                    return _instance;
                }

                var gameObject = new GameObject("HkVoiceMod.SettingsWindow");
                DontDestroyOnLoad(gameObject);
                _instance = gameObject.AddComponent<VoiceSettingsWindowController>();
                return _instance;
            }
        }

        internal void Show(HkVoiceMod mod, MenuScreen returnScreen)
        {
            _mod = mod ?? throw new ArgumentNullException(nameof(mod));
            _ = returnScreen ?? throw new ArgumentNullException(nameof(returnScreen));
            ResolveTheme(returnScreen);
            VoiceMacroCaptureService.Instance.Resolver.PrimeKeyboardMenuLabelCache();
            RememberNativeMenu(returnScreen);
            _draft = VoiceSettingsDraft.FromAppliedSettings(_mod.Settings);
            _stopThresholdText = FormatThresholdText(_draft.PendingStopKeywordConfig.KeywordThreshold);
            _recordingMacro = null;
            _delayMacro = null;
            _recordingStartSnapshot = null;
            _delayModalBlockedFrame = -1;
            _editingPairId = null;
            _editingDelayStepIndex = -1;
            _isClosingWindow = false;
            _hasLoggedRecordingTextProbe = false;
            CancelPendingReveal();
            CancelPendingTransition();

            EnsureBuilt();
            ResetWindowTransitionState();
            SetWindowPageVisible(true);
            ApplyThemeToExistingTree();
            EnsureEventSystem();
            VoiceMacroCaptureService.Instance.StopCapture();
            if (_recordingModal != null)
            {
                _recordingModal.SetActive(false);
            }

            if (_delayModal != null)
            {
                _delayModal.SetActive(false);
            }

            HideModalHostIfIdle();
            RebuildFromDraft();
            SetStatus("打开录制页后默认不会立即采集；点击“开始录制”后才会追加按下/松开事件。", false);
            BeginHideNativeMenuAndReveal();
        }

        internal void OpenFromMenu(HkVoiceMod mod, MenuScreen returnScreen)
        {
            if (IsVisible() || _pendingRevealCoroutine != null || _pendingTransitionCoroutine != null)
            {
                return;
            }

            Show(mod, returnScreen);
        }

        internal bool TryHandleMenuCancel()
        {
            if (!IsVisible())
            {
                return false;
            }

            RequestBack();
            return true;
        }

        private void Update()
        {
            if (!IsVisible())
            {
                return;
            }

            RefreshDynamicContent();

            if (_delayModal != null && _delayModal.activeInHierarchy)
            {
                if (_delayModalBlockedFrame == Time.frameCount)
                {
                    return;
                }

                if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    ConfirmDelayInput();
                }
                else if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                {
                    CancelDelayInput();
                }

                return;
            }

            if (_recordingModal != null && _recordingModal.activeInHierarchy)
            {
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                RequestBack();
            }
        }

        private void OnDestroy()
        {
            CancelPendingReveal();
            CancelPendingTransition();
            RestoreNativeMenu(false);

            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void EnsureBuilt()
        {
            if (_canvas != null)
            {
                return;
            }

            _font = _theme?.PrimaryFont ?? ResolveFont();

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 5000;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            SetVisible(false);

            var root = CreatePanel(gameObject.transform, "Root", FullscreenDimColor);
            StretchToParent(root);

            var window = CreatePanel(root.transform, "Window", WindowColor);
            _windowCanvasGroup = window.AddComponent<CanvasGroup>();
            var windowRect = window.GetComponent<RectTransform>();
            windowRect.anchorMin = new Vector2(0.5f, 0.5f);
            windowRect.anchorMax = new Vector2(0.5f, 0.5f);
            windowRect.pivot = new Vector2(0.5f, 0.5f);
            windowRect.sizeDelta = new Vector2(WindowWidth, WindowHeight);
            windowRect.anchoredPosition = Vector2.zero;

            var windowLayout = window.AddComponent<VerticalLayoutGroup>();
            windowLayout.padding = new RectOffset(24, 24, 20, 20);
            windowLayout.spacing = SectionSpacing;
            windowLayout.childControlWidth = true;
            windowLayout.childControlHeight = true;
            windowLayout.childForceExpandHeight = false;
            windowLayout.childForceExpandWidth = true;

            var topOrnamentSection = CreateSection(window.transform, "TopOrnamentSection", OrnamentSectionHeight);
            _windowTopOrnamentCanvasGroup = AddVisualCanvasGroup(topOrnamentSection);
            var header = CreatePanel(window.transform, "HeaderContent", WindowColor);
            _windowHeaderCanvasGroup = AddVisualCanvasGroup(header);
            AddLayoutElement(header, -1f, 128f, 0f);
            var headerLayout = header.AddComponent<VerticalLayoutGroup>();
            headerLayout.padding = new RectOffset(16, 16, 10, 10);
            headerLayout.spacing = 6f;
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandHeight = false;
            headerLayout.childForceExpandWidth = true;

            CreateText(header.transform, "Title", "语音设置", 36, TextColor, FontStyle.Bold, TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 42f);
            CreateText(header.transform, "Subtitle", "在这里设置停止词和语音宏。", 20, MutedTextColor, FontStyle.Normal, TextAnchor.UpperLeft, TextAnchor.MiddleLeft, 22f);
            _statusText = CreateText(header.transform, "Status", string.Empty, 20, MutedTextColor, FontStyle.Normal, TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 22f);

            AddLayoutElement(CreatePanel(window.transform, "StopDividerLine", new Color(0.78f, 0.72f, 0.58f, 0.30f)), -1f, 2f, 0f);

            var stopSection = CreatePanel(window.transform, "StopContent", WindowColor);
            _windowStopContentCanvasGroup = AddVisualCanvasGroup(stopSection);
            AddLayoutElement(stopSection, -1f, 112f, 0f);
            var stopLayout = stopSection.AddComponent<VerticalLayoutGroup>();
            stopLayout.padding = new RectOffset(16, 16, 10, 10);
            stopLayout.spacing = 8f;
            stopLayout.childControlWidth = true;
            stopLayout.childControlHeight = true;
            stopLayout.childForceExpandHeight = false;
            stopLayout.childForceExpandWidth = true;

            CreateText(stopSection.transform, "StopTitle", "停止词", 26, TextColor, FontStyle.Bold, TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 30f);
            var stopRow = CreateHorizontalGroup(stopSection.transform, "StopRow", 14f, FieldHeight);
            var stopRowLayout = stopRow.GetComponent<HorizontalLayoutGroup>();
            stopRowLayout.childControlWidth = true;
            CreateText(stopRow.transform, "StopWakeWordLabel", "停止词", 22, TextColor, FontStyle.Bold, TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, FieldHeight, false, 132f);
            CreateInput(stopRow.transform, "StopWakeWord", "输入停止词", StopWakeWordWidth, InputField.ContentType.Standard, value =>
            {
                if (_draft == null)
                {
                    return;
                }

                _draft.PendingStopKeywordConfig.WakeWord = value ?? string.Empty;
            }, out _stopWakeWordInput);
            var stopWakeWordLayout = _stopWakeWordInput.GetComponent<LayoutElement>();
            if (stopWakeWordLayout != null)
            {
                stopWakeWordLayout.flexibleWidth = 1f;
            }

            CreateText(stopRow.transform, "StopThresholdLabel", "阈值", 22, TextColor, FontStyle.Bold, TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, FieldHeight, false, 92f);
            CreateInput(stopRow.transform, "StopThreshold", "输入阈值", StopThresholdWidth, InputField.ContentType.DecimalNumber, value =>
            {
                if (_draft == null)
                {
                    return;
                }

                _stopThresholdText = (value ?? string.Empty).Trim();
                if (_stopThresholdText.Length == 0)
                {
                    _draft.PendingStopKeywordConfig.KeywordThreshold = -1f;
                    return;
                }

                if (TryParseFloat(_stopThresholdText, out var threshold))
                {
                    _draft.PendingStopKeywordConfig.KeywordThreshold = threshold;
                    return;
                }

                _draft.PendingStopKeywordConfig.KeywordThreshold = -1f;
            }, out _stopThresholdInput);

            var macroSection = CreateSection(window.transform, "MacroSection", -1f, true);
            _windowMacroCanvasGroup = AddVisualCanvasGroup(macroSection);
            var macroLayout = macroSection.AddComponent<VerticalLayoutGroup>();
            macroLayout.padding = new RectOffset(16, 16, 12, 12);
            macroLayout.spacing = 8f;
            macroLayout.childControlWidth = true;
            macroLayout.childControlHeight = true;
            macroLayout.childForceExpandHeight = false;
            macroLayout.childForceExpandWidth = true;
            AddLayoutElement(macroSection, -1f, -1f, 1f);

            CreateText(macroSection.transform, "MacroTitle", "语音宏", 26, TextColor, FontStyle.Bold, TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 30f);
            CreateText(macroSection.transform, "MacroHint", "设置唤醒词、阈值，并录制要执行的操作。", 18, MutedTextColor, FontStyle.Normal, TextAnchor.UpperLeft, TextAnchor.MiddleLeft, 22f);

            var macroScroll = CreateScrollView(macroSection.transform, "MacroScroll", out _macroListContent, 0f);
            AddLayoutElement(macroScroll, -1f, -1f, 1f);

            var footer = CreatePanel(window.transform, "FooterContent", WindowColor);
            _windowFooterCanvasGroup = AddVisualCanvasGroup(footer);
            AddLayoutElement(footer, -1f, 74f, 0f);
            var footerLayout = CreateHorizontalLayout(footer.transform, 10f, new RectOffset(12, 12, 12, 12));
            footerLayout.childAlignment = TextAnchor.MiddleRight;

            CreateButton(footer.transform, "AddMacroButton", "新增语音宏", SecondaryButtonWidth, SecondaryButtonColor, AddMacro, out _, true);
            CreateSpacer(footer.transform, "FooterSpacer", 1f);
            CreateButton(footer.transform, "ApplyButton", "保存", PrimaryButtonWidth, PrimaryButtonColor, ApplyCurrentDraft, out _, true);
            CreateButton(footer.transform, "DiscardButton", "放弃更改", DiscardButtonWidth, DangerButtonColor, DiscardAndClose, out _, true);
            CreateButton(footer.transform, "BackButton", "返回", PrimaryButtonWidth, SecondaryButtonColor, RequestBack, out _, true);

            var bottomOrnamentSection = CreateSection(window.transform, "BottomOrnamentSection", OrnamentSectionHeight);
            _windowBottomOrnamentCanvasGroup = AddVisualCanvasGroup(bottomOrnamentSection);

            _modalHost = CreatePanel(root.transform, "ModalHost", new Color(0f, 0f, 0f, 0.55f));
            StretchToParent(_modalHost);
            _modalHost.SetActive(false);

            _recordingModal = BuildRecordingModal(_modalHost.transform);
            _delayModal = BuildDelayModal(_modalHost.transform);

            SetVisible(false);
        }

        private GameObject BuildRecordingModal(Transform parent)
        {
            var modal = CreatePanel(parent, "RecordingModal", ModalColor);
            _recordingModalCanvasGroup = modal.AddComponent<CanvasGroup>();
            var modalRect = modal.GetComponent<RectTransform>();
            modalRect.anchorMin = new Vector2(0.5f, 0.5f);
            modalRect.anchorMax = new Vector2(0.5f, 0.5f);
            modalRect.pivot = new Vector2(0.5f, 0.5f);
            modalRect.sizeDelta = new Vector2(ModalWidth, ModalHeight);
            modalRect.anchoredPosition = Vector2.zero;

            var modalLayout = modal.AddComponent<VerticalLayoutGroup>();
            modalLayout.padding = new RectOffset(24, 24, 22, 22);
            modalLayout.spacing = 16f;
            modalLayout.childControlWidth = true;
            modalLayout.childControlHeight = true;
            modalLayout.childForceExpandWidth = true;
            modalLayout.childForceExpandHeight = false;

            _recordingTitleText = CreateText(modal.transform, "RecordingTitle", "录制宏", 34, TextColor, FontStyle.Bold, TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 44f);
            _recordingModalTitleCanvasGroup = AddVisualCanvasGroup(_recordingTitleText.gameObject);
            _recordingHintText = CreateText(modal.transform, "RecordingHint", "进入录制页后请先点击“开始录制”；停止录制时可使用 Backspace / Enter / Esc。", 24, MutedTextColor, FontStyle.Normal, TextAnchor.UpperLeft, TextAnchor.MiddleLeft, -1f);
            _recordingModalHintCanvasGroup = AddVisualCanvasGroup(_recordingHintText.gameObject);
            ConfigureWrappedAutoHeightText(_recordingHintText, 34f);

            var previewPanel = CreatePanel(modal.transform, "RecordingPreviewPanel", SectionColor);
            _recordingModalPreviewCanvasGroup = AddVisualCanvasGroup(previewPanel);
            AddLayoutElement(previewPanel, -1f, 220f, 1f);
            var previewLayout = previewPanel.AddComponent<VerticalLayoutGroup>();
            previewLayout.padding = new RectOffset(16, 16, 16, 16);
            previewLayout.spacing = 10f;
            previewLayout.childControlWidth = true;
            previewLayout.childControlHeight = true;
            previewLayout.childForceExpandWidth = true;
            previewLayout.childForceExpandHeight = false;

            CreateText(previewPanel.transform, "RecordingPreviewLabel", "当前完整事件序列", 24, TextColor, FontStyle.Bold, TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 30f);
            var previewScroll = CreateScrollView(previewPanel.transform, "RecordingPreviewScroll", out var previewContent, 0f);
            AddLayoutElement(previewScroll, -1f, -1f, 1f);
            _recordingStepListContent = previewContent;
            _recordingEmptyText = CreateText(previewContent, "RecordingEmptyText", "暂无事件；点击“开始录制”后会逐行显示。", 22, MutedTextColor, FontStyle.Normal, TextAnchor.UpperLeft, TextAnchor.UpperLeft, -1f);
            ConfigureWrappedAutoHeightText(_recordingEmptyText, 36f);

            _recordingStatusText = CreateText(modal.transform, "RecordingStatus", string.Empty, 24, MutedTextColor, FontStyle.Normal, TextAnchor.UpperLeft, TextAnchor.MiddleLeft, -1f);
            _recordingModalStatusCanvasGroup = AddVisualCanvasGroup(_recordingStatusText.gameObject);
            ConfigureWrappedAutoHeightText(_recordingStatusText, 60f);

            var actionRow = CreateHorizontalGroup(modal.transform, "RecordingActions", 16f, 60f, new RectOffset(0, 0, 4, 0));
            _recordingModalActionsCanvasGroup = AddVisualCanvasGroup(actionRow);
            actionRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;
            CreateSpacer(actionRow.transform, "RecordingActionSpacer", 1f);
            CreateButton(actionRow.transform, "RecordingStart", "开始录制", SecondaryButtonWidth, PrimaryButtonColor, StartRecordingFromButton, out _recordingStartButton, out _, true);
            CreateButton(actionRow.transform, "RecordingStop", "停止录制", SecondaryButtonWidth, SecondaryButtonColor, StopRecordingFromButton, out _recordingStopButton, out _, true);
            CreateButton(actionRow.transform, "RecordingClear", "清空", SecondaryButtonWidth, DangerButtonColor, ClearRecordingFromButton, out _recordingClearButton, out _, true);
            CreateButton(actionRow.transform, "RecordingConfirm", "确认", SecondaryButtonWidth, PrimaryButtonColor, ConfirmRecordingFromButton, out _recordingConfirmButton, out _, true);
            CreateButton(actionRow.transform, "RecordingCancel", "取消", SecondaryButtonWidth, SecondaryButtonColor, CancelRecordingFromButton, out _recordingCancelButton, out _, true);

            modal.SetActive(false);
            return modal;
        }

        private GameObject BuildDelayModal(Transform parent)
        {
            var modal = CreatePanel(parent, "DelayModal", ModalColor);
            var modalRect = modal.GetComponent<RectTransform>();
            modalRect.anchorMin = new Vector2(0.5f, 0.5f);
            modalRect.anchorMax = new Vector2(0.5f, 0.5f);
            modalRect.pivot = new Vector2(0.5f, 0.5f);
            modalRect.sizeDelta = new Vector2(DelayModalWidth, DelayModalHeight);
            modalRect.anchoredPosition = Vector2.zero;

            var modalLayout = modal.AddComponent<VerticalLayoutGroup>();
            modalLayout.padding = new RectOffset(24, 24, 22, 22);
            modalLayout.spacing = 16f;
            modalLayout.childControlWidth = true;
            modalLayout.childControlHeight = true;
            modalLayout.childForceExpandWidth = true;
            modalLayout.childForceExpandHeight = false;

            _delayTitleText = CreateText(modal.transform, "DelayTitle", "添加延迟", 32, TextColor, FontStyle.Bold, TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 40f);
            var delayHintText = CreateText(modal.transform, "DelayHint", "输入毫秒数，确认后会插入到当前录制里。", 22, MutedTextColor, FontStyle.Normal, TextAnchor.UpperLeft, TextAnchor.MiddleLeft, -1f);
            ConfigureWrappedAutoHeightText(delayHintText, 46f);

            _delayInputField = CreateLabeledInput(modal.transform, "DelayInput", "延迟（毫秒）", 280f, InputField.ContentType.IntegerNumber, value =>
            {
                if (_draft == null || _delayMacro == null)
                {
                    return;
                }

                var trimmed = (value ?? string.Empty).Trim();
                if (trimmed.Length == 0)
                {
                    _draft.SetPendingDelayMilliseconds(_delayMacro.Id, 0);
                    return;
                }

                if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds))
                {
                    _draft.SetPendingDelayMilliseconds(_delayMacro.Id, milliseconds);
                    return;
                }

                _draft.SetPendingDelayMilliseconds(_delayMacro.Id, 0);
            });

            _delayHintText = CreateText(modal.transform, "DelayPreview", string.Empty, 22, TextColor, FontStyle.Normal, TextAnchor.UpperLeft, TextAnchor.MiddleLeft, -1f);
            ConfigureWrappedAutoHeightText(_delayHintText, 30f);

            var actionRow = CreateHorizontalGroup(modal.transform, "DelayActions", 16f, 60f, new RectOffset(0, 0, 4, 0));
            actionRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;
            CreateSpacer(actionRow.transform, "DelayActionSpacer", 1f);
            CreateButton(actionRow.transform, "DelayConfirm", "确认", SecondaryButtonWidth, PrimaryButtonColor, ConfirmDelayInput, out _);
            CreateButton(actionRow.transform, "DelayCancel", "取消", SecondaryButtonWidth, SecondaryButtonColor, CancelDelayInput, out _);

            modal.SetActive(false);
            return modal;
        }

        private void RebuildRecordingStepRows()
        {
            if (_recordingStepListContent == null)
            {
                return;
            }

            for (var index = 0; index < _recordingStepRows.Count; index++)
            {
                if (_recordingStepRows[index].Root != null)
                {
                    Destroy(_recordingStepRows[index].Root);
                }
            }

            _recordingStepRows.Clear();
            _recordingTimelineRows.Clear();

            if (_recordingMacro == null)
            {
                return;
            }

            _recordingTimelineRows.AddRange(BuildRecordingTimelineRows(_recordingMacro));
            for (var index = 0; index < _recordingTimelineRows.Count; index++)
            {
                var row = BuildRecordingStepRow(_recordingStepListContent, _recordingTimelineRows[index], index);
                _recordingStepRows.Add(row);
            }
        }

        private RecordingStepRowWidgets BuildRecordingStepRow(RectTransform parent, RecordingTimelineRowModel rowModel, int rowIndex)
        {
            var rowRoot = CreatePanel(parent, $"RecordingStep-{rowIndex}", RowColor);
            AddLayoutElement(rowRoot, -1f, FieldHeight, 0f);

            var rowLayout = CreateHorizontalLayout(rowRoot.transform, 12f, new RectOffset(12, 12, 8, 8));
            rowLayout.childControlWidth = true;

            var indexText = CreateText(rowRoot.transform, $"RecordingStepIndex-{rowIndex}", $"{rowIndex + 1}.", 20, TextColor, FontStyle.Bold, TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, FieldHeight, false, 56f);
            var kindText = CreateText(rowRoot.transform, $"RecordingStepKind-{rowIndex}", string.Empty, 20, MutedTextColor, FontStyle.Bold, TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, FieldHeight, false, 80f);
            CreateButton(rowRoot.transform, $"RecordingStepValue-{rowIndex}", string.Empty, -1f, SecondaryButtonColor, () => HandleRecordingStepClick(rowIndex), out var valueButton, out var valueText, true);

            var buttonLayout = valueButton.GetComponent<LayoutElement>();
            if (buttonLayout != null)
            {
                buttonLayout.flexibleWidth = 1f;
                buttonLayout.minWidth = 420f;
            }

            var valueLabelRect = valueText.rectTransform;
            valueLabelRect.offsetMin = new Vector2(64f, 1f);
            valueLabelRect.offsetMax = new Vector2(valueLabelRect.offsetMax.x, -1f);
            valueText.alignment = TextAnchor.MiddleLeft;
            valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
            valueText.verticalOverflow = VerticalWrapMode.Truncate;
            valueText.resizeTextForBestFit = false;

            return new RecordingStepRowWidgets
            {
                Root = rowRoot,
                IndexText = indexText,
                KindText = kindText,
                ValueButton = valueButton,
                ValueText = valueText
            };
        }

        private void RefreshRecordingStepRows()
        {
            if (_recordingStepListContent == null || _recordingEmptyText == null)
            {
                return;
            }

            var timelineRows = _recordingMacro != null ? BuildRecordingTimelineRows(_recordingMacro) : new List<RecordingTimelineRowModel>();
            var rowCount = timelineRows.Count;
            var shouldRebuild = _recordingStepRows.Count != rowCount || _recordingTimelineRows.Count != rowCount;
            if (!shouldRebuild)
            {
                for (var index = 0; index < timelineRows.Count; index++)
                {
                    if (_recordingTimelineRows[index].IsDelayRow != timelineRows[index].IsDelayRow
                        || _recordingTimelineRows[index].EventIndex != timelineRows[index].EventIndex)
                    {
                        shouldRebuild = true;
                        break;
                    }
                }
            }

            if (shouldRebuild)
            {
                RebuildRecordingStepRows();
                timelineRows = _recordingMacro != null ? BuildRecordingTimelineRows(_recordingMacro) : new List<RecordingTimelineRowModel>();
            }

            _recordingTimelineRows.Clear();
            _recordingTimelineRows.AddRange(timelineRows);

            _recordingEmptyText.gameObject.SetActive(rowCount == 0);
            if (_recordingMacro == null)
            {
                return;
            }

            var canEdit = !VoiceMacroCaptureService.Instance.IsCapturing(_recordingMacro.Id)
                && !VoiceMacroCaptureService.Instance.IsCaptureSuspended(_recordingMacro.Id)
                && string.IsNullOrEmpty(_editingPairId);

            for (var index = 0; index < _recordingStepRows.Count; index++)
            {
                var rowModel = _recordingTimelineRows[index];
                var keyEvent = _recordingMacro.KeyEvents[rowModel.EventIndex];
                var row = _recordingStepRows[index];
                row.IndexText.text = $"{index + 1}.";
                row.KindText.text = rowModel.IsDelayRow
                    ? "间隔"
                    : (keyEvent.EventKind == VoiceMacroKeyEventKind.Down ? "按下" : "松开");
                row.ValueText.text = !rowModel.IsDelayRow && !string.IsNullOrEmpty(_editingPairId) && string.Equals(_editingPairId, rowModel.PairId, StringComparison.Ordinal)
                    ? "按下新的游戏按键..."
                    : BuildRecordingStepValueText(rowModel, keyEvent, VoiceMacroCaptureService.Instance.Resolver);
                row.ValueButton.interactable = canEdit;
            }

            ScheduleRecordingTextProbe();
        }

        private void HandleRecordingStepClick(int rowIndex)
        {
            if (_recordingMacro == null || rowIndex < 0 || rowIndex >= _recordingTimelineRows.Count)
            {
                return;
            }

            if (VoiceMacroCaptureService.Instance.IsCapturing(_recordingMacro.Id)
                || VoiceMacroCaptureService.Instance.IsCaptureSuspended(_recordingMacro.Id)
                || !string.IsNullOrEmpty(_editingPairId))
            {
                return;
            }

            var rowModel = _recordingTimelineRows[rowIndex];
            if (rowModel.IsDelayRow)
            {
                OpenDelayModalForStep(rowModel.EventIndex);
                return;
            }

            BeginActionStepEdit(rowModel.PairId);
        }

        private void BeginActionStepEdit(string pairId)
        {
            if (_recordingMacro == null || string.IsNullOrWhiteSpace(pairId))
            {
                return;
            }

            CancelPendingActionStepEdit();
            _editingPairId = pairId;
            VoiceMacroCaptureService.Instance.BeginSingleKeyCapture(
                _recordingMacro.Id,
                actionButton => ApplyActionStepEdit(pairId, actionButton),
                CancelActionStepEdit);
            RefreshDynamicContent();
        }

        private void ApplyActionStepEdit(string pairId, global::GlobalEnums.HeroActionButton actionButton)
        {
            if (_recordingMacro == null || string.IsNullOrWhiteSpace(pairId))
            {
                _editingPairId = null;
                RefreshDynamicContent();
                return;
            }

            for (var index = 0; index < _recordingMacro.KeyEvents.Count; index++)
            {
                if (string.Equals(_recordingMacro.KeyEvents[index].PairId, pairId, StringComparison.Ordinal))
                {
                    _recordingMacro.KeyEvents[index].ActionButton = actionButton;
                }
            }

            _editingPairId = null;
            RefreshDynamicContent();
        }

        private void CancelActionStepEdit()
        {
            _editingPairId = null;
            RefreshDynamicContent();
        }

        private void CancelPendingActionStepEdit()
        {
            if (_recordingMacro == null || string.IsNullOrWhiteSpace(_editingPairId))
            {
                _editingPairId = null;
                return;
            }

            VoiceMacroCaptureService.Instance.CancelSingleKeyCapture(_recordingMacro.Id);
            _editingPairId = null;
        }

        private void OpenDelayModalForStep(int stepIndex)
        {
            if (_draft == null || _recordingMacro == null || _delayModal == null || _recordingModal == null || stepIndex <= 0 || stepIndex >= _recordingMacro.KeyEvents.Count)
            {
                return;
            }

            CancelPendingActionStepEdit();
            _editingDelayStepIndex = stepIndex;
            _delayMacro = _recordingMacro;
            VoiceMacroCaptureService.Instance.SuspendCapture();

            var milliseconds = Math.Max(0, _recordingMacro.KeyEvents[stepIndex].DelayBeforeMilliseconds);
            _draft.SetPendingDelayMilliseconds(_delayMacro.Id, milliseconds);
            SetInputFieldText(_delayInputField, milliseconds > 0 ? milliseconds.ToString(CultureInfo.InvariantCulture) : string.Empty);
            _delayModalBlockedFrame = Time.frameCount;

            if (_delayTitleText != null)
            {
                _delayTitleText.text = "编辑延迟";
            }

            if (_recordingModal != null)
            {
                _recordingModal.SetActive(false);
            }

            if (_delayModal != null)
            {
                _delayModal.SetActive(true);
            }

            RefreshDynamicContent();
            FocusInputField(_delayInputField);
        }

        private List<RecordingTimelineRowModel> BuildRecordingTimelineRows(VoiceMacroConfig macro)
        {
            var rows = new List<RecordingTimelineRowModel>();
            if (macro.KeyEvents == null)
            {
                return rows;
            }

            for (var index = 0; index < macro.KeyEvents.Count; index++)
            {
                var keyEvent = macro.KeyEvents[index];
                if (keyEvent == null)
                {
                    continue;
                }

                if (index > 0)
                {
                    rows.Add(new RecordingTimelineRowModel
                    {
                        IsDelayRow = true,
                        EventIndex = index,
                        PairId = keyEvent.PairId
                    });
                }

                rows.Add(new RecordingTimelineRowModel
                {
                    IsDelayRow = false,
                    EventIndex = index,
                    PairId = keyEvent.PairId
                });
            }

            return rows;
        }

        private static string BuildRecordingStepValueText(RecordingTimelineRowModel rowModel, VoiceMacroKeyEvent keyEvent, GameKeybindNameResolver resolver)
        {
            if (rowModel.IsDelayRow)
            {
                return $"{Math.Max(0, keyEvent.DelayBeforeMilliseconds)} 毫秒";
            }

            return BuildRecordingActionValueText(keyEvent.ActionButton, resolver);
        }

        private static string BuildRecordingActionValueText(global::GlobalEnums.HeroActionButton actionButton, GameKeybindNameResolver resolver)
        {
            return resolver.GetDisplayName(actionButton);
        }

        private void ScheduleRecordingTextProbe()
        {
            if (_hasLoggedRecordingTextProbe || !isActiveAndEnabled)
            {
                return;
            }

            StartCoroutine(LogRecordingTextProbeAtEndOfFrame());
        }

        private IEnumerator LogRecordingTextProbeAtEndOfFrame()
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            LogRecordingTextProbeOnce();
        }

        private void LogRecordingTextProbeOnce()
        {
            if (_hasLoggedRecordingTextProbe || _mod == null || _recordingStepRows.Count == 0)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            if (_recordingStepListContent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_recordingStepListContent);
            }

            var firstRow = _recordingStepRows[0];
            var rowButton = firstRow.ValueButton;
            var rowStyle = rowButton != null ? rowButton.GetComponent<SettingsPageButtonStyle>() : null;
            var rowFirstText = rowButton != null ? rowButton.GetComponentInChildren<Text>(true) : null;
            var rowTexts = rowButton != null ? rowButton.GetComponentsInChildren<Text>(true) : Array.Empty<Text>();

            var referenceButton = _recordingStartButton;
            var referenceStyle = referenceButton != null ? referenceButton.GetComponent<SettingsPageButtonStyle>() : null;
            var referenceFirstText = referenceButton != null ? referenceButton.GetComponentInChildren<Text>(true) : null;
            var referenceTexts = referenceButton != null ? referenceButton.GetComponentsInChildren<Text>(true) : Array.Empty<Text>();

            var builder = new StringBuilder();
            builder.AppendLine("[UIProbe] 录制行文字探针");
            builder.AppendLine(DescribeButtonProbe("row.button", rowButton, rowStyle, rowFirstText, rowTexts));
            builder.AppendLine(DescribeTextProbe("row.ValueText", firstRow.ValueText));
            builder.AppendLine(DescribeTextProbe("row.style.LabelText", rowStyle != null ? rowStyle.LabelText : null));
            builder.AppendLine(DescribeTextProbe("row.firstText", rowFirstText));
            builder.AppendLine(DescribeButtonProbe("ref.button", referenceButton, referenceStyle, referenceFirstText, referenceTexts));
            builder.AppendLine(DescribeTextProbe("ref.style.LabelText", referenceStyle != null ? referenceStyle.LabelText : null));
            builder.AppendLine(DescribeTextProbe("ref.firstText", referenceFirstText));

            _mod.LogWarn(builder.ToString().TrimEnd());
            _hasLoggedRecordingTextProbe = true;
        }

        private static string DescribeButtonProbe(string tag, Button? button, SettingsPageButtonStyle? style, Text? firstText, Text[] texts)
        {
            if (button == null)
            {
                return $"{tag}: <null button>";
            }

            var image = button.targetGraphic as Image;
            var rect = button.GetComponent<RectTransform>();
            var childNames = new StringBuilder();
            for (var index = 0; index < button.transform.childCount; index++)
            {
                if (index > 0)
                {
                    childNames.Append(", ");
                }

                childNames.Append(button.transform.GetChild(index).name);
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}: name={1}, interactable={2}, active={3}, imageColor=({4:0.###},{5:0.###},{6:0.###},{7:0.###}), rect=({8:0.##}x{9:0.##}), styleLabel={10}, firstText={11}, textCount={12}, children=[{13}]",
                tag,
                button.gameObject.name,
                button.interactable,
                button.gameObject.activeInHierarchy,
                image != null ? image.color.r : -1f,
                image != null ? image.color.g : -1f,
                image != null ? image.color.b : -1f,
                image != null ? image.color.a : -1f,
                rect != null ? rect.rect.width : -1f,
                rect != null ? rect.rect.height : -1f,
                style != null && style.LabelText != null ? style.LabelText.gameObject.name : "<null>",
                firstText != null ? firstText.gameObject.name : "<null>",
                texts.Length,
                childNames.ToString());
        }

        private static string DescribeTextProbe(string tag, Text? text)
        {
            if (text == null)
            {
                return $"{tag}: <null text>";
            }

            var rect = text.rectTransform;
            var parentName = text.transform.parent != null ? text.transform.parent.name : "<no-parent>";
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}: name={1}, text='{2}', enabled={3}, active={4}, color=({5:0.###},{6:0.###},{7:0.###},{8:0.###}), font={9}, size={10}, align={11}, rect=({12:0.##}x{13:0.##}), preferred=({14:0.##}x{15:0.##}), charsVisible={16}, resizeBestFit={17}, bestFitRange=({18}-{19}), scale=({20:0.###},{21:0.###},{22:0.###}), offsetMin=({23:0.##},{24:0.##}), offsetMax=({25:0.##},{26:0.##}), parent={27}, sibling={28}, maskChain={29}",
                tag,
                text.gameObject.name,
                (text.text ?? string.Empty).Replace("\r", "\\r").Replace("\n", "\\n"),
                text.enabled,
                text.gameObject.activeInHierarchy,
                text.color.r,
                text.color.g,
                text.color.b,
                text.color.a,
                text.font != null ? text.font.name : "<null>",
                text.fontSize,
                text.alignment,
                rect.rect.width,
                rect.rect.height,
                text.preferredWidth,
                text.preferredHeight,
                text.cachedTextGenerator.characterCountVisible,
                text.resizeTextForBestFit,
                text.resizeTextMinSize,
                text.resizeTextMaxSize,
                text.transform.lossyScale.x,
                text.transform.lossyScale.y,
                text.transform.lossyScale.z,
                rect.offsetMin.x,
                rect.offsetMin.y,
                rect.offsetMax.x,
                rect.offsetMax.y,
                parentName,
                text.transform.GetSiblingIndex(),
                DescribeMaskChain(text.transform));
        }

        private static string DescribeMaskChain(Transform transform)
        {
            var parts = new List<string>();
            var current = transform.parent;
            while (current != null)
            {
                var hasRectMask = current.GetComponent<RectMask2D>() != null;
                var hasMask = current.GetComponent<Mask>() != null;
                if (hasRectMask || hasMask)
                {
                    parts.Add($"{current.name}(RectMask2D={hasRectMask},Mask={hasMask})");
                }

                current = current.parent;
            }

            return parts.Count == 0 ? "<none>" : string.Join(" -> ", parts.ToArray());
        }

        private void RebuildFromDraft()
        {
            if (_draft == null || _macroListContent == null)
            {
                return;
            }

            SetInputFieldText(_stopWakeWordInput, _draft.PendingStopKeywordConfig.WakeWord ?? string.Empty);
            SetInputFieldText(_stopThresholdInput, _stopThresholdText);

            for (var index = 0; index < _macroRows.Count; index++)
            {
                if (_macroRows[index].Root != null)
                {
                    Destroy(_macroRows[index].Root);
                }
            }

            _macroRows.Clear();

            for (var index = 0; index < _draft.PendingMacroConfigs.Count; index++)
            {
                var macro = _draft.PendingMacroConfigs[index];
                var row = BuildMacroRow(_macroListContent, macro, index + 1);
                _macroRows.Add(row);
            }
        }

        private MacroRowWidgets BuildMacroRow(RectTransform parent, VoiceMacroConfig macro, int displayIndex)
        {
            var rowRoot = CreatePanel(parent, $"MacroRow-{macro.Id}", RowColor);
            AddLayoutElement(rowRoot, -1f, -1f, 0f);
            AddContentSizeFitter(rowRoot, ContentSizeFitter.FitMode.Unconstrained, ContentSizeFitter.FitMode.PreferredSize);

            var rowLayout = rowRoot.AddComponent<VerticalLayoutGroup>();
            rowLayout.padding = new RectOffset(14, 14, 10, 10);
            rowLayout.spacing = 6f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;

            var topRow = CreateHorizontalGroup(rowRoot.transform, $"MacroTop-{macro.Id}", RowSpacing, FieldHeight);
            var topRowLayout = topRow.GetComponent<HorizontalLayoutGroup>();
            topRowLayout.childControlWidth = true;
            CreateText(topRow.transform, $"MacroLabel-{macro.Id}", $"宏 {displayIndex}", 20, TextColor, FontStyle.Bold, TextAnchor.MiddleCenter, TextAnchor.MiddleCenter, FieldHeight, false, 76f);
            CreateInput(topRow.transform, $"MacroWakeWord-{macro.Id}", "唤醒词", MacroWakeWordWidth, InputField.ContentType.Standard, value =>
            {
                if (_draft == null)
                {
                    return;
                }

                _draft.SelectMacro(macro.Id);
                macro.WakeWord = value ?? string.Empty;
            }, out var wakeWordInput);
            var wakeWordLayout = wakeWordInput.GetComponent<LayoutElement>();
            if (wakeWordLayout != null)
            {
                wakeWordLayout.flexibleWidth = 1f;
            }

            SetInputFieldText(wakeWordInput, macro.WakeWord ?? string.Empty);

            CreateInput(topRow.transform, $"MacroThreshold-{macro.Id}", "阈值", MacroThresholdWidth, InputField.ContentType.DecimalNumber, value =>
            {
                if (_draft == null)
                {
                    return;
                }

                _draft.SelectMacro(macro.Id);
                _draft.SetPendingThresholdText(macro.Id, value);
            }, out var thresholdInput);
            SetInputFieldText(thresholdInput, _draft != null ? _draft.GetPendingThresholdText(macro.Id) : string.Empty);

            CreateButton(topRow.transform, $"MacroRecord-{macro.Id}", "录制", SecondaryButtonWidth, PrimaryButtonColor, () => OpenRecordingModal(macro), out var recordButtonText, true);
            CreateButton(topRow.transform, $"MacroDelete-{macro.Id}", "删除", DeleteButtonWidth, DangerButtonColor, () => DeleteMacro(macro.Id), out _, true);

            var summaryText = CreateText(rowRoot.transform, $"MacroSummary-{macro.Id}", string.Empty, 20, MutedTextColor, FontStyle.Normal, TextAnchor.UpperLeft, TextAnchor.UpperLeft, -1f);
            summaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            ConfigureWrappedAutoHeightText(summaryText, 24f);

            return new MacroRowWidgets
            {
                Macro = macro,
                Root = rowRoot,
                SummaryText = summaryText,
                RecordButtonText = recordButtonText
            };
        }

        private void AddMacro()
        {
            if (_draft == null)
            {
                return;
            }

            _draft.AddMacro(VoiceSettingsMenuBuilder.CreateNewMacro());
            RebuildFromDraft();
            SetStatus("已新增一个空白宏草稿。", false);
        }

        private void DeleteMacro(string macroId)
        {
            if (_draft == null)
            {
                return;
            }

            VoiceMacroCaptureService.Instance.StopCapture();
            _draft.RemoveMacro(macroId);
            RebuildFromDraft();
            SetStatus("已删除所选宏草稿。", false);
        }

        private void ApplyCurrentDraft()
        {
            if (_mod == null || _draft == null)
            {
                return;
            }

            if (VoiceSettingsMenuBuilder.TryApplyDraft(_mod, _draft))
            {
                SetStatus("已保存新的语音宏、停止词和阈值设置。", false, true);
                return;
            }

            SetStatus("保存失败：请检查空唤醒词、无效阈值或空事件序列。", true);
        }

        private void RequestBack()
        {
            if (_isClosingWindow)
            {
                return;
            }

            if (_delayModal != null && _delayModal.activeInHierarchy)
            {
                CancelDelayInput();
                return;
            }

            if (_recordingModal != null && _recordingModal.activeInHierarchy)
            {
                CancelRecordingFromButton();
                return;
            }

            if (_mod == null || _draft == null)
            {
                CloseWindow();
                return;
            }

            if (!_draft.HasPendingChanges(_mod.Settings))
            {
                CloseWindow();
                return;
            }

            if (VoiceSettingsMenuBuilder.TryApplyDraft(_mod, _draft))
            {
                CloseWindow();
                return;
            }

            SetStatus("返回失败：自动保存未成功。你可以继续修改，或点击“放弃更改”直接关闭。", true);
        }

        private void DiscardAndClose()
        {
            CloseWindow();
        }

        private void OpenRecordingModal(VoiceMacroConfig macro)
        {
            if (_draft == null || _recordingModal == null || _modalHost == null || _recordingTitleText == null)
            {
                return;
            }

            CancelPendingTransition();
            VoiceMacroCaptureService.Instance.StopCapture();
            _recordingMacro = macro;
            _recordingStartSnapshot = _draft.CloneMacroKeyEvents(macro.Id);
            _editingPairId = null;
            _editingDelayStepIndex = -1;
            _hasLoggedRecordingTextProbe = false;
            _recordingTitleText.text = $"录制宏：{VoiceSettingsMenuBuilder.GetMacroDisplayName(macro)}";

            PrepareRecordingModalRevealTransitionState();
            _modalHost.SetActive(true);
            _recordingModal.SetActive(true);
            if (_delayModal != null)
            {
                _delayModal.SetActive(false);
            }

            SetWindowPageVisible(false);

            VoiceMacroCaptureService.Instance.BeginCapture(
                macro.Id,
                keyEvent => macro.KeyEvents.Add(new VoiceMacroKeyEvent
                {
                    DelayBeforeMilliseconds = keyEvent.DelayBeforeMilliseconds,
                    ActionButton = keyEvent.ActionButton,
                    EventKind = keyEvent.EventKind,
                    PairId = keyEvent.PairId
                }),
                () =>
                {
                    if (macro.KeyEvents.Count > 0)
                    {
                        macro.KeyEvents.RemoveAt(macro.KeyEvents.Count - 1);
                    }
                },
                () => CloseRecordingModal(false, true),
                () => CloseRecordingModal(true, true));

            RefreshDynamicContent();
            _pendingTransitionCoroutine = StartCoroutine(OpenRecordingModalWithTransition());
        }

        private void StartRecordingFromButton()
        {
            if (_recordingMacro == null)
            {
                return;
            }

            CancelPendingActionStepEdit();
            _editingDelayStepIndex = -1;
            VoiceMacroCaptureService.Instance.StartCapture(_recordingMacro.Id);
            RefreshDynamicContent();
        }

        private void StopRecordingFromButton()
        {
            if (_recordingMacro == null)
            {
                return;
            }

            CancelPendingActionStepEdit();
            VoiceMacroCaptureService.Instance.StopActiveCapture(_recordingMacro.Id);
            RefreshDynamicContent();
        }

        private void ClearRecordingFromButton()
        {
            if (_recordingMacro == null)
            {
                return;
            }

            CancelPendingActionStepEdit();
            _editingDelayStepIndex = -1;
            VoiceMacroCaptureService.Instance.StopActiveCapture(_recordingMacro.Id);
            _recordingMacro.KeyEvents.Clear();
            RefreshDynamicContent();
        }

        private void ConfirmRecordingFromButton()
        {
            CloseRecordingModal(true, true);
        }

        private void CancelRecordingFromButton()
        {
            CloseRecordingModal(false, true);
        }

        private void CloseRecordingModal(bool applyChanges, bool stopCapture)
        {
            if (_pendingTransitionCoroutine != null)
            {
                return;
            }

            if (_draft != null && _recordingMacro != null && !applyChanges && _recordingStartSnapshot != null)
            {
                _draft.ReplaceMacroKeyEvents(_recordingMacro.Id, _recordingStartSnapshot);
            }

            if (stopCapture)
            {
                VoiceMacroCaptureService.Instance.StopCapture();
            }

            _recordingMacro = null;
            _recordingStartSnapshot = null;
            _editingPairId = null;
            _editingDelayStepIndex = -1;
            _hasLoggedRecordingTextProbe = false;

            _pendingTransitionCoroutine = StartCoroutine(CloseRecordingModalWithTransition());
        }

        private void ConfirmDelayInput()
        {
            if (_draft == null || _delayMacro == null || _delayInputField == null)
            {
                return;
            }

            var trimmed = (_delayInputField.text ?? string.Empty).Trim();
            if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds) || milliseconds < 0)
            {
                SetStatus("延迟必须是大于等于 0 的整数毫秒。", true);
                return;
            }

            _draft.SetPendingDelayMilliseconds(_delayMacro.Id, milliseconds);
            if (_editingDelayStepIndex > 0 && _editingDelayStepIndex < _delayMacro.KeyEvents.Count)
            {
                _delayMacro.KeyEvents[_editingDelayStepIndex].DelayBeforeMilliseconds = milliseconds;
                CloseDelayModal(true);
                return;
            }

            CloseDelayModal(true);
        }

        private void CancelDelayInput()
        {
            CloseDelayModal(false);
        }

        private void CloseDelayModal(bool appendDelay)
        {
            if (_delayModal != null)
            {
                _delayModal.SetActive(false);
            }

            if (_recordingModal != null)
            {
                _recordingModal.SetActive(true);
            }

            _delayMacro = null;
            _delayModalBlockedFrame = -1;
            _editingDelayStepIndex = -1;
            VoiceMacroCaptureService.Instance.ResumeCapture();
            RefreshDynamicContent();
            if (!appendDelay)
            {
                SelectGameObject(null);
            }
        }

        private void CloseWindow()
        {
            if (_isClosingWindow)
            {
                return;
            }

            _isClosingWindow = true;
            CancelPendingReveal();
            CancelPendingTransition();
            VoiceMacroCaptureService.Instance.StopCapture();
            _recordingMacro = null;
            _delayMacro = null;
            _recordingStartSnapshot = null;
            _delayModalBlockedFrame = -1;
            _editingPairId = null;
            _editingDelayStepIndex = -1;

            if (_delayModal != null)
            {
                _delayModal.SetActive(false);
            }

            if (_recordingModal != null)
            {
                _recordingModal.SetActive(false);
            }

            HideModalHostIfIdle();
            _pendingTransitionCoroutine = StartCoroutine(CloseWindowWithTransition());
        }

        private void RememberNativeMenu(MenuScreen returnScreen)
        {
            RestoreNativeMenu(false);

            _hiddenNativeMenuScreen = returnScreen;
            _hiddenNativeMenuWasActive = returnScreen.gameObject.activeSelf;
        }

        private void RestoreNativeMenu(bool animated)
        {
            var hiddenNativeMenuScreen = _hiddenNativeMenuScreen;
            var hiddenNativeMenuWasActive = _hiddenNativeMenuWasActive;
            _hiddenNativeMenuScreen = null;
            _hiddenNativeMenuWasActive = false;

            if (!hiddenNativeMenuWasActive || hiddenNativeMenuScreen == null)
            {
                return;
            }

            if (animated && UIManager.instance != null)
            {
                StartCoroutine(UIManager.instance.ShowMenu(hiddenNativeMenuScreen));
                return;
            }

            var hiddenNativeMenuObject = hiddenNativeMenuScreen.gameObject;
            if (hiddenNativeMenuObject != null)
            {
                hiddenNativeMenuObject.SetActive(true);
            }
        }

        private void RefreshDynamicContent()
        {
            for (var index = 0; index < _macroRows.Count; index++)
            {
                var row = _macroRows[index];
                row.RecordButtonText.text = VoiceMacroCaptureService.Instance.IsCapturing(row.Macro.Id) ? "录制中" : "录制";
                row.SummaryText.text = BuildMacroSummaryText(row.Macro);
            }

            if (_recordingMacro != null)
            {
                var isRecording = VoiceMacroCaptureService.Instance.IsCapturing(_recordingMacro.Id);
                var hasSession = VoiceMacroCaptureService.Instance.HasCaptureSession(_recordingMacro.Id);
                var isSuspended = VoiceMacroCaptureService.Instance.IsCaptureSuspended(_recordingMacro.Id);
                var isAwaitingSingleKeyInput = VoiceMacroCaptureService.Instance.IsAwaitingSingleKeyInput(_recordingMacro.Id);

                if (_recordingHintText != null)
                {
                    if (isAwaitingSingleKeyInput && !string.IsNullOrWhiteSpace(_editingPairId))
                    {
                        _recordingHintText.text = "正在修改一对按下/松开事件：请按新的游戏按键，按 Esc 取消。";
                    }
                    else
                    {
                        _recordingHintText.text = isRecording
                            ? "正在录制：会记录按下、松开和两事件之间的间隔；如需删除、确认或取消，请先点击“停止录制”。"
                            : "当前未录制：点击“开始录制”后才会追加事件；停止录制时可以点击下方事件或间隔进行编辑。";
                    }
                }

                if (_recordingStartButton != null)
                {
                    _recordingStartButton.interactable = hasSession && !isRecording && !isSuspended && !isAwaitingSingleKeyInput;
                }

                if (_recordingStopButton != null)
                {
                    _recordingStopButton.interactable = hasSession && isRecording && !isAwaitingSingleKeyInput;
                }

                if (_recordingClearButton != null)
                {
                    _recordingClearButton.interactable = hasSession && !isRecording && !isAwaitingSingleKeyInput && _recordingMacro.KeyEvents.Count > 0;
                }

                if (_recordingConfirmButton != null)
                {
                    _recordingConfirmButton.interactable = hasSession && !isRecording;
                }

                if (_recordingCancelButton != null)
                {
                    _recordingCancelButton.interactable = hasSession && !isRecording;
                }

                RefreshRecordingStepRows();

                if (_recordingStatusText != null)
                {
                    _recordingStatusText.text = isAwaitingSingleKeyInput && !string.IsNullOrWhiteSpace(_editingPairId)
                        ? "等待新的动作输入：当前这一对按下/松开事件会在收到按键后同步替换。"
                        : VoiceMacroCaptureService.Instance.GetStatusText(_recordingMacro.Id);
                }
            }

            if (_draft != null && _delayMacro != null && _delayHintText != null)
            {
                var pendingMilliseconds = _draft.GetPendingDelayMilliseconds(_delayMacro.Id);
                _delayHintText.text = _editingDelayStepIndex >= 0
                    ? $"当前将修改为：{pendingMilliseconds} 毫秒"
                    : $"当前将插入：{pendingMilliseconds} 毫秒";
            }
        }

        private string BuildMacroSummaryText(VoiceMacroConfig macro)
        {
            var prefix = VoiceMacroCaptureService.Instance.IsCapturing(macro.Id) ? "录制中：" : "当前宏：";
            return prefix + VoiceSettingsMenuBuilder.FormatMacroEventSequence(macro, VoiceMacroCaptureService.Instance.Resolver);
        }

        private void SetStatus(string message, bool isError, bool isSuccess = false)
        {
            if (_statusText == null)
            {
                return;
            }

            _statusText.text = message ?? string.Empty;
            var theme = _theme;
            _statusText.color = isError
                ? theme?.ErrorTextColor ?? ErrorTextColor
                : (isSuccess ? theme?.SuccessTextColor ?? SuccessTextColor : theme?.MutedTextColor ?? MutedTextColor);
        }

        private void SetVisible(bool visible)
        {
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = visible;
        }

        private static CanvasGroup AddVisualCanvasGroup(GameObject gameObject)
        {
            var canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            return canvasGroup;
        }

        private static void ResetVisualCanvasGroup(CanvasGroup? canvasGroup)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.gameObject.SetActive(true);
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        private static void PrepareVisualCanvasGroupForReveal(CanvasGroup? canvasGroup)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.gameObject.SetActive(true);
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void ResetWindowTransitionState()
        {
            ResetVisualCanvasGroup(_windowTopOrnamentCanvasGroup);
            ResetVisualCanvasGroup(_windowHeaderCanvasGroup);
            ResetVisualCanvasGroup(_windowStopContentCanvasGroup);
            ResetVisualCanvasGroup(_windowMacroCanvasGroup);
            ResetVisualCanvasGroup(_windowFooterCanvasGroup);
            ResetVisualCanvasGroup(_windowBottomOrnamentCanvasGroup);

            if (_windowCanvasGroup != null)
            {
                _windowCanvasGroup.gameObject.SetActive(true);
                _windowCanvasGroup.alpha = 1f;
                _windowCanvasGroup.interactable = true;
                _windowCanvasGroup.blocksRaycasts = true;
            }
        }

        private void PrepareWindowRevealTransitionState()
        {
            PrepareVisualCanvasGroupForReveal(_windowTopOrnamentCanvasGroup);
            PrepareVisualCanvasGroupForReveal(_windowHeaderCanvasGroup);
            PrepareVisualCanvasGroupForReveal(_windowStopContentCanvasGroup);
            PrepareVisualCanvasGroupForReveal(_windowMacroCanvasGroup);
            PrepareVisualCanvasGroupForReveal(_windowFooterCanvasGroup);
            PrepareVisualCanvasGroupForReveal(_windowBottomOrnamentCanvasGroup);

            if (_windowCanvasGroup != null)
            {
                _windowCanvasGroup.gameObject.SetActive(true);
                _windowCanvasGroup.alpha = 0f;
                _windowCanvasGroup.interactable = false;
                _windowCanvasGroup.blocksRaycasts = false;
            }
        }

        private void ResetRecordingModalTransitionState()
        {
            ResetVisualCanvasGroup(_recordingModalTitleCanvasGroup);
            ResetVisualCanvasGroup(_recordingModalHintCanvasGroup);
            ResetVisualCanvasGroup(_recordingModalPreviewCanvasGroup);
            ResetVisualCanvasGroup(_recordingModalStatusCanvasGroup);
            ResetVisualCanvasGroup(_recordingModalActionsCanvasGroup);

            if (_recordingModalCanvasGroup != null)
            {
                _recordingModalCanvasGroup.gameObject.SetActive(true);
                _recordingModalCanvasGroup.alpha = 1f;
                _recordingModalCanvasGroup.interactable = true;
                _recordingModalCanvasGroup.blocksRaycasts = true;
            }
        }

        private void PrepareRecordingModalRevealTransitionState()
        {
            PrepareVisualCanvasGroupForReveal(_recordingModalTitleCanvasGroup);
            PrepareVisualCanvasGroupForReveal(_recordingModalHintCanvasGroup);
            PrepareVisualCanvasGroupForReveal(_recordingModalPreviewCanvasGroup);
            PrepareVisualCanvasGroupForReveal(_recordingModalStatusCanvasGroup);
            PrepareVisualCanvasGroupForReveal(_recordingModalActionsCanvasGroup);

            if (_recordingModalCanvasGroup != null)
            {
                _recordingModalCanvasGroup.gameObject.SetActive(true);
                _recordingModalCanvasGroup.alpha = 0f;
                _recordingModalCanvasGroup.interactable = false;
                _recordingModalCanvasGroup.blocksRaycasts = false;
            }
        }

        private float GetMenuFadeSpeed()
        {
            return UIManager.instance != null ? UIManager.instance.MENU_FADE_SPEED : DefaultMenuFadeSpeed;
        }

        private IEnumerator FadeInCanvasGroup(CanvasGroup? canvasGroup)
        {
            if (canvasGroup == null)
            {
                yield break;
            }

            var fadeSpeed = GetMenuFadeSpeed();
            var loopFailsafe = 0f;
            canvasGroup.gameObject.SetActive(true);
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            while (canvasGroup.alpha < 0.95f)
            {
                canvasGroup.alpha += Time.unscaledDeltaTime * fadeSpeed;
                loopFailsafe += Time.unscaledDeltaTime;
                if (canvasGroup.alpha >= 0.95f || loopFailsafe >= 2f)
                {
                    break;
                }

                yield return null;
            }

            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        private IEnumerator FadeOutCanvasGroup(CanvasGroup? canvasGroup, bool deactivateAtEnd = false)
        {
            if (canvasGroup == null)
            {
                yield break;
            }

            var fadeSpeed = GetMenuFadeSpeed();
            var loopFailsafe = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            while (canvasGroup.alpha > 0.05f)
            {
                canvasGroup.alpha -= Time.unscaledDeltaTime * fadeSpeed;
                loopFailsafe += Time.unscaledDeltaTime;
                if (canvasGroup.alpha <= 0.05f || loopFailsafe >= 2f)
                {
                    break;
                }

                yield return null;
            }

            canvasGroup.alpha = 0f;
            if (deactivateAtEnd)
            {
                canvasGroup.gameObject.SetActive(false);
            }
        }

        private static IEnumerator WaitMenuTransitionStage()
        {
            yield return new WaitForSecondsRealtime(MenuTransitionStageDelay);
        }

        private void BeginRevealAfterLayoutSettles()
        {
            CancelPendingReveal();
            SetVisible(false);

            if (!isActiveAndEnabled)
            {
                FinalizeReveal();
                return;
            }

            _pendingRevealCoroutine = StartCoroutine(RevealAfterLayoutSettles());
        }

        private IEnumerator RevealAfterLayoutSettles()
        {
            for (var pass = 0; pass < 2; pass++)
            {
                ForceRefreshWindowLayout();
                yield return null;
                yield return new WaitForEndOfFrame();
            }

            ForceRefreshWindowLayout();
            PrepareWindowRevealTransitionState();
            SetVisible(true);
            yield return StartCoroutine(ShowWindowWithMenuEffect());
            _pendingRevealCoroutine = null;
            SelectGameObject(null);
        }

        private void FinalizeReveal()
        {
            ForceRefreshWindowLayout();
            SetVisible(true);
            SelectGameObject(null);
        }

        private void ForceRefreshWindowLayout()
        {
            Canvas.ForceUpdateCanvases();

            if (_windowCanvasGroup != null)
            {
                var windowRect = _windowCanvasGroup.GetComponent<RectTransform>();
                if (windowRect != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(windowRect);
                }
            }

            if (_macroListContent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_macroListContent);
            }

            Canvas.ForceUpdateCanvases();
        }

        private void CancelPendingReveal()
        {
            if (_pendingRevealCoroutine == null)
            {
                return;
            }

            StopCoroutine(_pendingRevealCoroutine);
            _pendingRevealCoroutine = null;
        }

        private void CancelPendingTransition()
        {
            if (_pendingTransitionCoroutine == null)
            {
                return;
            }

            StopCoroutine(_pendingTransitionCoroutine);
            _pendingTransitionCoroutine = null;
        }

        private void BeginHideNativeMenuAndReveal()
        {
            CancelPendingTransition();

            var hiddenNativeMenuScreen = _hiddenNativeMenuScreen;
            if (!_hiddenNativeMenuWasActive || hiddenNativeMenuScreen == null)
            {
                BeginRevealAfterLayoutSettles();
                return;
            }

            var uiManager = UIManager.instance;
            if (uiManager == null)
            {
                hiddenNativeMenuScreen.gameObject.SetActive(false);
                BeginRevealAfterLayoutSettles();
                return;
            }

            _pendingTransitionCoroutine = StartCoroutine(HideNativeMenuAndReveal(hiddenNativeMenuScreen, uiManager));
        }

        private IEnumerator HideNativeMenuAndReveal(MenuScreen menuScreen, UIManager uiManager)
        {
            yield return StartCoroutine(uiManager.HideMenu(menuScreen));
            _pendingTransitionCoroutine = null;
            BeginRevealAfterLayoutSettles();
        }

        private IEnumerator ShowWindowWithMenuEffect()
        {
            if (_windowCanvasGroup != null)
            {
                StartCoroutine(FadeInCanvasGroup(_windowCanvasGroup));
            }

            if (_windowHeaderCanvasGroup != null)
            {
                StartCoroutine(FadeInCanvasGroup(_windowHeaderCanvasGroup));
            }

            yield return StartCoroutine(WaitMenuTransitionStage());

            if (_windowTopOrnamentCanvasGroup != null)
            {
                StartCoroutine(FadeInCanvasGroup(_windowTopOrnamentCanvasGroup));
            }

            yield return StartCoroutine(WaitMenuTransitionStage());

            if (_windowStopContentCanvasGroup != null)
            {
                StartCoroutine(FadeInCanvasGroup(_windowStopContentCanvasGroup));
            }

            if (_windowMacroCanvasGroup != null)
            {
                StartCoroutine(FadeInCanvasGroup(_windowMacroCanvasGroup));
            }

            if (_windowFooterCanvasGroup != null)
            {
                StartCoroutine(FadeInCanvasGroup(_windowFooterCanvasGroup));
            }

            if (_windowBottomOrnamentCanvasGroup != null)
            {
                StartCoroutine(FadeInCanvasGroup(_windowBottomOrnamentCanvasGroup));
            }

            yield return StartCoroutine(WaitMenuTransitionStage());
        }

        private IEnumerator ShowRecordingModalWithMenuEffect()
        {
            if (_recordingModalCanvasGroup != null)
            {
                StartCoroutine(FadeInCanvasGroup(_recordingModalCanvasGroup));
            }

            if (_recordingModalTitleCanvasGroup != null)
            {
                StartCoroutine(FadeInCanvasGroup(_recordingModalTitleCanvasGroup));
            }

            yield return StartCoroutine(WaitMenuTransitionStage());

            if (_recordingModalHintCanvasGroup != null)
            {
                StartCoroutine(FadeInCanvasGroup(_recordingModalHintCanvasGroup));
            }

            if (_recordingModalPreviewCanvasGroup != null)
            {
                StartCoroutine(FadeInCanvasGroup(_recordingModalPreviewCanvasGroup));
            }

            if (_recordingModalStatusCanvasGroup != null)
            {
                StartCoroutine(FadeInCanvasGroup(_recordingModalStatusCanvasGroup));
            }

            yield return StartCoroutine(WaitMenuTransitionStage());

            if (_recordingModalActionsCanvasGroup != null)
            {
                StartCoroutine(FadeInCanvasGroup(_recordingModalActionsCanvasGroup));
            }

            yield return StartCoroutine(WaitMenuTransitionStage());
        }

        private IEnumerator HideWindowWithMenuEffect()
        {
            if (_windowHeaderCanvasGroup != null)
            {
                StartCoroutine(FadeOutCanvasGroup(_windowHeaderCanvasGroup));
            }

            yield return StartCoroutine(WaitMenuTransitionStage());

            if (_windowTopOrnamentCanvasGroup != null)
            {
                StartCoroutine(FadeOutCanvasGroup(_windowTopOrnamentCanvasGroup));
            }

            yield return StartCoroutine(WaitMenuTransitionStage());

            if (_windowStopContentCanvasGroup != null)
            {
                StartCoroutine(FadeOutCanvasGroup(_windowStopContentCanvasGroup));
            }

            if (_windowMacroCanvasGroup != null)
            {
                StartCoroutine(FadeOutCanvasGroup(_windowMacroCanvasGroup));
            }

            if (_windowFooterCanvasGroup != null)
            {
                StartCoroutine(FadeOutCanvasGroup(_windowFooterCanvasGroup));
            }

            yield return StartCoroutine(WaitMenuTransitionStage());

            if (_windowBottomOrnamentCanvasGroup != null)
            {
                StartCoroutine(FadeOutCanvasGroup(_windowBottomOrnamentCanvasGroup));
            }

            yield return StartCoroutine(FadeOutCanvasGroup(_windowCanvasGroup));
        }

        private IEnumerator HideRecordingModalWithMenuEffect()
        {
            if (_recordingModalTitleCanvasGroup != null)
            {
                StartCoroutine(FadeOutCanvasGroup(_recordingModalTitleCanvasGroup));
            }

            yield return StartCoroutine(WaitMenuTransitionStage());

            if (_recordingModalHintCanvasGroup != null)
            {
                StartCoroutine(FadeOutCanvasGroup(_recordingModalHintCanvasGroup));
            }

            if (_recordingModalPreviewCanvasGroup != null)
            {
                StartCoroutine(FadeOutCanvasGroup(_recordingModalPreviewCanvasGroup));
            }

            if (_recordingModalStatusCanvasGroup != null)
            {
                StartCoroutine(FadeOutCanvasGroup(_recordingModalStatusCanvasGroup));
            }

            yield return StartCoroutine(WaitMenuTransitionStage());

            if (_recordingModalActionsCanvasGroup != null)
            {
                StartCoroutine(FadeOutCanvasGroup(_recordingModalActionsCanvasGroup));
            }

            yield return StartCoroutine(FadeOutCanvasGroup(_recordingModalCanvasGroup));
        }

        private IEnumerator CloseWindowWithTransition()
        {
            yield return StartCoroutine(HideWindowWithMenuEffect());
            SetVisible(false);
            SetWindowPageVisible(true);
            ResetWindowTransitionState();
            RestoreNativeMenu(true);
            SelectGameObject(null);
            _isClosingWindow = false;
            _pendingTransitionCoroutine = null;
        }

        private IEnumerator OpenRecordingModalWithTransition()
        {
            yield return StartCoroutine(ShowRecordingModalWithMenuEffect());
            _pendingTransitionCoroutine = null;
        }

        private IEnumerator CloseRecordingModalWithTransition()
        {
            yield return StartCoroutine(HideRecordingModalWithMenuEffect());

            ResetRecordingModalTransitionState();

            if (_delayModal != null)
            {
                _delayModal.SetActive(false);
            }

            if (_recordingModal != null)
            {
                _recordingModal.SetActive(false);
            }

            SetWindowPageVisible(true);
            HideModalHostIfIdle();
            RefreshDynamicContent();
            _pendingTransitionCoroutine = null;
        }

        private void SetWindowPageVisible(bool visible)
        {
            if (_windowCanvasGroup == null)
            {
                return;
            }

            _windowCanvasGroup.alpha = visible ? 1f : 0f;
            _windowCanvasGroup.blocksRaycasts = visible;
            _windowCanvasGroup.interactable = visible;
        }

        private bool IsVisible()
        {
            return _canvasGroup != null && _canvasGroup.alpha > 0.01f;
        }

        private void HideModalHostIfIdle()
        {
            if (_modalHost == null)
            {
                return;
            }

            var recordingVisible = _recordingModal != null && _recordingModal.activeInHierarchy;
            var delayVisible = _delayModal != null && _delayModal.activeInHierarchy;
            _modalHost.SetActive(recordingVisible || delayVisible);
        }
        private bool IsEditingTextInput()
        {
            var current = EventSystem.current;
            if (current == null || current.currentSelectedGameObject == null)
            {
                return false;
            }

            return current.currentSelectedGameObject.GetComponentInParent<InputField>() != null;
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("HkVoiceMod.UI.EventSystem");
            DontDestroyOnLoad(eventSystemObject);
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private void SelectGameObject(GameObject? target)
        {
            if (EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(target);
        }

        private void FocusInputField(InputField? inputField)
        {
            if (inputField == null)
            {
                SelectGameObject(null);
                return;
            }

            SelectGameObject(inputField.gameObject);
            inputField.ActivateInputField();
            inputField.MoveTextEnd(false);
        }

        private Font ResolveFont()
        {
            var texts = Resources.FindObjectsOfTypeAll<Text>();
            for (var index = 0; index < texts.Length; index++)
            {
                var font = texts[index] != null ? texts[index].font : null;
                if (font != null)
                {
                    return font;
                }
            }

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private void ResolveTheme(MenuScreen returnScreen)
        {
            var fallbackFont = ResolveFont();
            var resolvedTheme = VoiceSettingsThemeResolver.Resolve(returnScreen, fallbackFont);
            if (_theme != null && _theme.SectionSprite != null && resolvedTheme.SectionSprite == null)
            {
                resolvedTheme = resolvedTheme.WithSectionSprite(_theme.SectionSprite, _theme.SectionSpriteIsSliced);
            }

            _theme = resolvedTheme;
            _font = _theme.PrimaryFont;
        }

        private void ApplyThemeToExistingTree()
        {
            if (_canvas == null || _theme == null)
            {
                return;
            }

            var buttons = _canvas.GetComponentsInChildren<Button>(true);
            for (var index = 0; index < buttons.Length; index++)
            {
                var button = buttons[index];
                if (button == null)
                {
                    continue;
                }

                var labelText = button.GetComponentInChildren<Text>(true);
                if (labelText == null)
                {
                    continue;
                }

                ApplyButtonTheme(button, labelText, ResolveButtonKind(button.gameObject.name, (button.targetGraphic as Image)?.color ?? SecondaryButtonColor));
            }

            var inputFields = _canvas.GetComponentsInChildren<InputField>(true);
            for (var index = 0; index < inputFields.Length; index++)
            {
                ApplyInputTheme(inputFields[index]);
            }

            var images = _canvas.GetComponentsInChildren<Image>(true);
            for (var index = 0; index < images.Length; index++)
            {
                var image = images[index];
                if (image == null
                    || image.GetComponent<Button>() != null
                    || image.GetComponent<InputField>() != null
                    || image.GetComponent<SettingsPageButtonOrnament>() != null
                    || string.Equals(image.gameObject.name, "Viewport", StringComparison.Ordinal))
                {
                    continue;
                }

                ApplyPanelTheme(image, image.gameObject.name, image.color);
            }

            var texts = _canvas.GetComponentsInChildren<Text>(true);
            for (var index = 0; index < texts.Length; index++)
            {
                var text = texts[index];
                if (text == null
                    || text.GetComponentInParent<Button>() != null
                    || text.GetComponentInParent<InputField>() != null)
                {
                    continue;
                }

                ApplyTextTheme(text, IsMutedTextName(text.gameObject.name));
            }

            if (_statusText != null)
            {
                ApplyTextTheme(_statusText, true);
            }
        }

        private void ApplyImageTheme(Image image, Sprite? sprite, bool useSliced, Color tint)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = sprite != null && useSliced ? Image.Type.Sliced : Image.Type.Simple;
            image.color = tint;
            image.rectTransform.localScale = Vector3.one;
            image.preserveAspect = false;
        }

        private void ApplyTextTheme(Text text, bool muted)
        {
            if (text == null)
            {
                return;
            }

            var theme = _theme;
            if (theme == null)
            {
                return;
            }

            text.font = theme.PrimaryFont;
            text.color = muted ? theme.MutedTextColor : theme.TextColor;
        }

        private void ApplyButtonTheme(Button button, Text labelText, VoiceThemeButtonKind kind)
        {
            if (button == null || _theme == null)
            {
                return;
            }

            button.transition = Selectable.Transition.ColorTint;
            switch (kind)
            {
                case VoiceThemeButtonKind.Primary:
                    button.colors = _theme.CreatePrimaryButtonColors();
                    break;
                case VoiceThemeButtonKind.Danger:
                    button.colors = _theme.CreateDangerButtonColors();
                    break;
                default:
                    button.colors = _theme.CreateSecondaryButtonColors();
                    break;
            }

            var settingsPageStyle = button.GetComponent<SettingsPageButtonStyle>();
            if (settingsPageStyle != null)
            {
                settingsPageStyle.Kind = kind;
                settingsPageStyle.ApplyTheme(_theme);
                return;
            }

            var image = button.targetGraphic as Image;
            if (image != null)
            {
                ApplyImageTheme(image, _theme.GetButtonSprite(kind), _theme.ButtonSpriteIsSliced, _theme.GetButtonTint(kind));
            }

            if (labelText != null)
            {
                labelText.font = _theme.PrimaryFont;
                labelText.color = _theme.TextColor;
            }
        }

        private void ApplyInputTheme(InputField inputField)
        {
            if (inputField == null || _theme == null)
            {
                return;
            }

            var image = inputField.targetGraphic as Image;
            if (image != null)
            {
                ApplyImageTheme(image, null, false, Color.clear);
            }

            inputField.transition = Selectable.Transition.ColorTint;
            inputField.colors = _theme.CreateInputColors();
            if (inputField.textComponent != null)
            {
                inputField.textComponent.font = _theme.PrimaryFont;
                inputField.textComponent.color = _theme.TextColor;
            }

            var placeholderText = inputField.placeholder as Text;
            if (placeholderText != null)
            {
                placeholderText.font = _theme.PrimaryFont;
                placeholderText.color = _theme.PlaceholderTextColor;
            }
        }

        private void ApplyPanelTheme(Image image, string name, Color fallbackColor)
        {
            if (_theme == null)
            {
                return;
            }

            if (string.Equals(name, "Root", StringComparison.Ordinal)
                || ColorsMatch(fallbackColor, FullscreenDimColor))
            {
                ApplyImageTheme(image, null, false, _theme.FullscreenDimColor);
                return;
            }

            if (name.EndsWith("DividerLine", StringComparison.Ordinal)
                || name.IndexOf("DividerLine", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ApplyImageTheme(image, null, false, fallbackColor);
                return;
            }

            if (string.Equals(name, "ModalHost", StringComparison.Ordinal) || fallbackColor.a < 0.75f)
            {
                ApplyImageTheme(
                    image,
                    null,
                    false,
                    new Color(_theme.FullscreenDimColor.r, _theme.FullscreenDimColor.g, _theme.FullscreenDimColor.b, fallbackColor.a));
                return;
            }

            if (string.Equals(name, "TopOrnamentSection", StringComparison.Ordinal)
                || string.Equals(name, "BottomOrnamentSection", StringComparison.Ordinal))
            {
                var ornamentSprite = _ornamentSectionSprite ?? image.sprite ?? _theme.SectionSprite;
                if (ornamentSprite != null)
                {
                    _ornamentSectionSprite = ornamentSprite;
                }

                ApplyImageTheme(image, ornamentSprite, false, _theme.SectionTint);
                image.rectTransform.localScale = new Vector3(1f, OrnamentVerticalScale, 1f);
                return;
            }

            if (string.Equals(name, "Window", StringComparison.Ordinal)
                || name.EndsWith("Modal", StringComparison.Ordinal)
                || ColorsMatch(fallbackColor, WindowColor)
                || ColorsMatch(fallbackColor, ModalColor))
            {
                ApplyImageTheme(image, null, false, Color.clear);
                return;
            }

            if (string.Equals(name, "MacroSection", StringComparison.Ordinal)
                || string.Equals(name, "RecordingPreviewPanel", StringComparison.Ordinal))
            {
                ApplyImageTheme(image, null, false, Color.clear);
                return;
            }

            if (name.StartsWith("MacroRow-", StringComparison.Ordinal)
                || name.StartsWith("RecordingStep-", StringComparison.Ordinal))
            {
                ApplyImageTheme(image, null, false, Color.clear);
                return;
            }

            if (name.IndexOf("Section", StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(name, "Header", StringComparison.Ordinal)
                || string.Equals(name, "Footer", StringComparison.Ordinal)
                || name.IndexOf("PreviewPanel", StringComparison.OrdinalIgnoreCase) >= 0
                || ColorsMatch(fallbackColor, SectionColor))
            {
                ApplyImageTheme(image, _theme.SectionSprite, _theme.SectionSpriteIsSliced, _theme.SectionTint);
                return;
            }

            if (name.IndexOf("Scroll", StringComparison.OrdinalIgnoreCase) >= 0
                || ColorsMatch(fallbackColor, InputColor))
            {
                ApplyImageTheme(image, null, false, Color.clear);
                return;
            }

            ApplyImageTheme(image, _theme.RowSprite, _theme.RowSpriteIsSliced, _theme.RowTint);
        }

        private static VoiceThemeButtonKind ResolveButtonKind(string name, Color fallbackColor)
        {
            if (name.IndexOf("Delete", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Discard", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Clear", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return VoiceThemeButtonKind.Danger;
            }

            if (name.IndexOf("Apply", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Confirm", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Start", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Record", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return VoiceThemeButtonKind.Primary;
            }

            return ResolveButtonKind(fallbackColor);
        }

        private static VoiceThemeButtonKind ResolveButtonKind(Color fallbackColor)
        {
            if (ColorsMatch(fallbackColor, DangerButtonColor))
            {
                return VoiceThemeButtonKind.Danger;
            }

            if (ColorsMatch(fallbackColor, PrimaryButtonColor))
            {
                return VoiceThemeButtonKind.Primary;
            }

            return VoiceThemeButtonKind.Secondary;
        }

        private static bool IsMutedTextName(string name)
        {
            return name.IndexOf("Subtitle", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Hint", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Summary", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Empty", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Status", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Kind", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ColorsMatch(Color left, Color right)
        {
            return Mathf.Abs(left.r - right.r) <= 0.01f
                && Mathf.Abs(left.g - right.g) <= 0.01f
                && Mathf.Abs(left.b - right.b) <= 0.01f
                && Mathf.Abs(left.a - right.a) <= 0.01f;
        }

        private static void StretchToParent(GameObject gameObject)
        {
            var rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private GameObject CreateSection(Transform parent, string name, float preferredHeight, bool flexibleHeight = false)
        {
            var section = CreatePanel(parent, name, SectionColor);
            AddLayoutElement(section, -1f, preferredHeight, flexibleHeight ? 1f : 0f);
            return section;
        }

        private static HorizontalLayoutGroup CreateHorizontalLayout(Transform parent, float spacing, RectOffset padding)
        {
            var layout = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = padding;
            layout.spacing = spacing;
            layout.childControlHeight = true;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            return layout;
        }

        private GameObject CreateHorizontalGroup(Transform parent, string name, float spacing, float preferredHeight, RectOffset? padding = null)
        {
            var group = new GameObject(name, typeof(RectTransform));
            group.transform.SetParent(parent, false);
            AddLayoutElement(group, -1f, preferredHeight, 0f);
            var layout = CreateHorizontalLayout(group.transform, spacing, padding ?? new RectOffset());
            layout.childControlHeight = true;
            return group;
        }

        private GameObject CreateScrollView(Transform parent, string name, out RectTransform content, float preferredHeight)
        {
            var scrollRoot = CreatePanel(parent, name, InputColor);
            AddLayoutElement(scrollRoot, -1f, preferredHeight > 0f ? preferredHeight : -1f, preferredHeight <= 0f ? 1f : 0f);

            var scrollRect = scrollRoot.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(scrollRoot.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            StretchToParent(viewport);
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);

            var contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewport.transform, false);
            content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);

            var layout = contentObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 8, 0, 0);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            AddContentSizeFitter(contentObject, ContentSizeFitter.FitMode.Unconstrained, ContentSizeFitter.FitMode.PreferredSize);

            scrollRect.viewport = viewportRect;
            scrollRect.content = content;
            return scrollRoot;
        }

        private InputField CreateLabeledInput(Transform parent, string name, string labelText, float width, InputField.ContentType contentType, Action<string> onValueChanged)
        {
            var group = CreateHorizontalGroup(parent, $"{name}Group", 10f, FieldHeight);
            CreateText(group.transform, $"{name}Label", labelText, 22, TextColor, FontStyle.Bold, TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, FieldHeight, false, 136f);
            CreateInput(group.transform, name, labelText, width, contentType, onValueChanged, out var inputField);
            return inputField;
        }

        private void CreateInput(Transform parent, string name, string placeholder, float width, InputField.ContentType contentType, Action<string> onValueChanged, out InputField inputField)
        {
            var inputObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            inputObject.transform.SetParent(parent, false);
            AddLayoutElement(inputObject, width, FieldHeight, 0f);

            var image = inputObject.GetComponent<Image>();

            inputField = inputObject.GetComponent<InputField>();
            inputField.targetGraphic = image;
            inputField.contentType = contentType;
            inputField.lineType = InputField.LineType.SingleLine;

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(inputObject.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 8f);
            textRect.offsetMax = new Vector2(-12f, -8f);

            var text = textObject.GetComponent<Text>();
            text.font = _theme?.PrimaryFont ?? _font;
            text.fontSize = 22;
            text.color = _theme?.TextColor ?? TextColor;
            text.alignment = TextAnchor.MiddleLeft;
            text.text = string.Empty;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false;
            text.raycastTarget = false;

            var placeholderObject = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
            placeholderObject.transform.SetParent(inputObject.transform, false);
            var placeholderRect = placeholderObject.GetComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(12f, 8f);
            placeholderRect.offsetMax = new Vector2(-12f, -8f);

            var placeholderText = placeholderObject.GetComponent<Text>();
            placeholderText.font = _theme?.PrimaryFont ?? _font;
            placeholderText.fontSize = 22;
            placeholderText.fontStyle = FontStyle.Italic;
            placeholderText.color = _theme?.PlaceholderTextColor ?? PlaceholderTextColor;
            placeholderText.alignment = TextAnchor.MiddleLeft;
            placeholderText.horizontalOverflow = HorizontalWrapMode.Overflow;
            placeholderText.verticalOverflow = VerticalWrapMode.Truncate;
            placeholderText.text = placeholder;
            placeholderText.raycastTarget = false;

            inputField.textComponent = text;
            inputField.placeholder = placeholderText;
            inputField.onValueChanged.AddListener(value => onValueChanged(value));
            ApplyInputTheme(inputField);
        }

        private void CreateButton(Transform parent, string name, string label, float width, Color backgroundColor, Action onClick, out Text labelText)
        {
            CreateButton(parent, name, label, width, backgroundColor, onClick, out _, out labelText, false);
        }

        private void CreateButton(Transform parent, string name, string label, float width, Color backgroundColor, Action onClick, out Text labelText, bool useSettingsPageStyle)
        {
            CreateButton(parent, name, label, width, backgroundColor, onClick, out _, out labelText, useSettingsPageStyle);
        }

        private void CreateButton(Transform parent, string name, string label, float width, Color backgroundColor, Action onClick, out Button button, out Text labelText)
        {
            CreateButton(parent, name, label, width, backgroundColor, onClick, out button, out labelText, false);
        }

        private void CreateButton(Transform parent, string name, string label, float width, Color backgroundColor, Action onClick, out Button button, out Text labelText, bool useSettingsPageStyle)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            AddLayoutElement(buttonObject, width, FieldHeight, 0f);

            var image = buttonObject.GetComponent<Image>();
            var kind = ResolveButtonKind(name, backgroundColor);

            button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());

            labelText = CreateText(buttonObject.transform, $"{name}Label", label, 22, TextColor, FontStyle.Bold, TextAnchor.MiddleCenter, TextAnchor.MiddleCenter, FieldHeight);
            StretchToParent(labelText.gameObject);
            var labelRect = labelText.GetComponent<RectTransform>();
            labelRect.offsetMin = new Vector2(useSettingsPageStyle ? 32f : 10f, 6f);
            labelRect.offsetMax = new Vector2(useSettingsPageStyle ? -32f : -10f, -6f);
            labelText.horizontalOverflow = HorizontalWrapMode.Wrap;
            labelText.verticalOverflow = VerticalWrapMode.Truncate;
            labelText.resizeTextForBestFit = true;
            labelText.resizeTextMinSize = 18;
            labelText.resizeTextMaxSize = 22;
            labelText.raycastTarget = false;

            if (useSettingsPageStyle)
            {
                var settingsPageStyle = buttonObject.AddComponent<SettingsPageButtonStyle>();
                settingsPageStyle.Button = button;
                settingsPageStyle.LabelText = labelText;
                settingsPageStyle.LeftArrow = CreateSettingsPageButtonOrnament(buttonObject.transform, $"{name}LeftArrow", false);
                settingsPageStyle.RightArrow = CreateSettingsPageButtonOrnament(buttonObject.transform, $"{name}RightArrow", true);
                settingsPageStyle.Kind = kind;
            }

            ApplyButtonTheme(button, labelText, kind);
        }

        private static SettingsPageButtonOrnament CreateSettingsPageButtonOrnament(Transform parent, string name, bool alignRight)
        {
            var ornament = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(SettingsPageButtonOrnament));
            ornament.transform.SetParent(parent, false);

            var ornamentRect = ornament.GetComponent<RectTransform>();
            ornamentRect.anchorMin = new Vector2(alignRight ? 1f : 0f, 0.5f);
            ornamentRect.anchorMax = ornamentRect.anchorMin;
            ornamentRect.pivot = new Vector2(0.5f, 0.5f);
            ornamentRect.sizeDelta = new Vector2(28f, 36f);
            ornamentRect.anchoredPosition = new Vector2(alignRight ? -18f : 18f, 0f);

            var ornamentImage = ornament.GetComponent<Image>();
            ornamentImage.raycastTarget = false;
            ornamentImage.fillCenter = false;

            var ornamentController = ornament.GetComponent<SettingsPageButtonOrnament>();
            ornamentController.Initialize(ornamentImage, alignRight);
            return ornamentController;
        }

        private GameObject CreatePanel(Transform parent, string name, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var image = panel.GetComponent<Image>();
            ApplyPanelTheme(image, name, color);
            return panel;
        }

        private Text CreateText(Transform parent, string name, string textValue, int fontSize, Color color, FontStyle fontStyle, TextAnchor alignment, TextAnchor childAlignment, float preferredHeight, bool flexibleHeight = false, float preferredWidth = -1f)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            AddLayoutElement(textObject, preferredWidth, preferredHeight, flexibleHeight ? 1f : 0f);

            var text = textObject.GetComponent<Text>();
            text.font = _theme?.PrimaryFont ?? _font;
            text.text = textValue;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            if (ColorsMatch(color, PlaceholderTextColor))
            {
                text.color = _theme?.PlaceholderTextColor ?? PlaceholderTextColor;
            }
            else if (ColorsMatch(color, ErrorTextColor))
            {
                text.color = _theme?.ErrorTextColor ?? ErrorTextColor;
            }
            else if (ColorsMatch(color, SuccessTextColor))
            {
                text.color = _theme?.SuccessTextColor ?? SuccessTextColor;
            }
            else
            {
                text.color = ColorsMatch(color, MutedTextColor)
                    ? _theme?.MutedTextColor ?? MutedTextColor
                    : _theme?.TextColor ?? TextColor;
            }
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;

            var layoutElement = textObject.GetComponent<LayoutElement>();
            if (layoutElement != null && preferredWidth < 0f)
            {
                layoutElement.minWidth = 0f;
            }

            return text;
        }

        private void ConfigureWrappedAutoHeightText(Text text, float minHeight = 0f)
        {
            AddContentSizeFitter(text.gameObject, ContentSizeFitter.FitMode.Unconstrained, ContentSizeFitter.FitMode.PreferredSize);
            var layoutElement = text.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.minHeight = minHeight;
                layoutElement.flexibleHeight = 0f;
            }
        }

        private static void AddLayoutElement(GameObject gameObject, float preferredWidth, float preferredHeight, float flexibleHeight)
        {
            var layoutElement = gameObject.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = gameObject.AddComponent<LayoutElement>();
            }

            if (preferredWidth >= 0f)
            {
                layoutElement.preferredWidth = preferredWidth;
            }

            if (preferredHeight >= 0f)
            {
                layoutElement.preferredHeight = preferredHeight;
            }

            layoutElement.flexibleHeight = flexibleHeight;
        }

        private static void AddContentSizeFitter(GameObject gameObject, ContentSizeFitter.FitMode horizontalFit, ContentSizeFitter.FitMode verticalFit)
        {
            var fitter = gameObject.GetComponent<ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = gameObject.AddComponent<ContentSizeFitter>();
            }

            fitter.horizontalFit = horizontalFit;
            fitter.verticalFit = verticalFit;
        }

        private static void SetInputFieldText(InputField? inputField, string text)
        {
            if (inputField == null)
            {
                return;
            }

            inputField.SetTextWithoutNotify(text ?? string.Empty);
        }

        private static void CreateSpacer(Transform parent, string name, float flexibleWidth)
        {
            var spacer = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(parent, false);
            var layout = spacer.GetComponent<LayoutElement>();
            layout.flexibleWidth = flexibleWidth;
            layout.minHeight = 1f;
        }

        private static bool TryParseFloat(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private static string FormatThresholdText(float threshold)
        {
            if (!float.IsNaN(threshold) && !float.IsInfinity(threshold) && threshold >= 0.01f && threshold <= 1.0f)
            {
                return threshold.ToString("0.##", CultureInfo.InvariantCulture);
            }

            return string.Empty;
        }

        private sealed class MacroRowWidgets
        {
            public VoiceMacroConfig Macro { get; set; } = null!;

            public GameObject Root { get; set; } = null!;

            public Text SummaryText { get; set; } = null!;

            public Text RecordButtonText { get; set; } = null!;
        }

        private sealed class RecordingStepRowWidgets
        {
            public GameObject Root { get; set; } = null!;

            public Text IndexText { get; set; } = null!;

            public Text KindText { get; set; } = null!;

            public Button ValueButton { get; set; } = null!;

            public Text ValueText { get; set; } = null!;
        }

        private sealed class RecordingTimelineRowModel
        {
            public bool IsDelayRow { get; set; }

            public int EventIndex { get; set; }

            public string PairId { get; set; } = string.Empty;
        }

        private sealed class SettingsPageButtonOrnament : MonoBehaviour
        {
            private const float StaticWidth = 28f;
            private const float StaticHeight = 36f;
            private const float StaticOffsetX = 18f;
            private const float NativeWidth = 164f;
            private const float NativeHeight = 119f;
            private const float NativeOffsetX = 30f;
            private const float NativeScale = 0.22f;

            private Image? _image;
            private Animator? _animator;
            private bool _alignRight;
            private float _edgeOffsetX;

            public void Initialize(Image image, bool alignRight)
            {
                _image = image ?? throw new ArgumentNullException(nameof(image));
                _alignRight = alignRight;
                _edgeOffsetX = alignRight ? NativeOffsetX : NativeOffsetX;
                ApplyStaticLayout();
                TryEnableNativeAnimator();
            }

            public float GetVisualHalfWidth()
            {
                return _animator != null
                    ? NativeWidth * NativeScale * 0.5f
                    : StaticWidth * 0.5f;
            }

            public void SetEdgeOffset(float offsetX)
            {
                _edgeOffsetX = Mathf.Max(0f, offsetX);
                ApplyCurrentOffset();
            }

            public void ApplyTheme(VoiceSettingsTheme theme, VoiceThemeButtonKind kind)
            {
                if (_image == null)
                {
                    return;
                }

                TryEnableNativeAnimator();
                if (_animator != null)
                {
                    ApplyNativeLayout();
                    _image.enabled = true;
                    _image.sprite = null;
                    _image.type = Image.Type.Simple;
                    _image.color = Color.white;
                    _image.preserveAspect = false;
                    return;
                }

                ApplyStaticLayout();
                var sprite = theme.GetButtonSprite(kind);
                _image.sprite = sprite;
                _image.type = sprite != null && theme.ButtonSpriteIsSliced ? Image.Type.Sliced : Image.Type.Simple;
                _image.color = Color.white;
                _image.preserveAspect = false;
                _image.fillCenter = false;
                _image.enabled = false;
            }

            public void SetVisible(bool visible)
            {
                if (_image == null)
                {
                    return;
                }

                if (_animator != null)
                {
                    if (visible)
                    {
                        _animator.ResetTrigger("hide");
                        _animator.SetTrigger("show");
                    }
                    else
                    {
                        _animator.ResetTrigger("show");
                        _animator.SetTrigger("hide");
                    }

                    return;
                }

                _image.enabled = visible;
            }

            private void TryEnableNativeAnimator()
            {
                if (_animator != null)
                {
                    return;
                }

                MenuResources.ReloadResources();
                if (MenuResources.MenuCursorAnimator == null)
                {
                    return;
                }

                _animator = gameObject.GetComponent<Animator>();
                if (_animator == null)
                {
                    _animator = gameObject.AddComponent<Animator>();
                }

                _animator.runtimeAnimatorController = MenuResources.MenuCursorAnimator;
                _animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                _animator.applyRootMotion = false;
                ApplyNativeLayout();
            }

            private void ApplyStaticLayout()
            {
                if (_image == null)
                {
                    return;
                }

                var rect = _image.rectTransform;
                rect.sizeDelta = new Vector2(StaticWidth, StaticHeight);
                rect.localScale = new Vector3(_alignRight ? -1f : 1f, 1f, 1f);
                if (_edgeOffsetX <= 0f)
                {
                    _edgeOffsetX = StaticOffsetX;
                }

                ApplyCurrentOffset();
            }

            private void ApplyNativeLayout()
            {
                if (_image == null)
                {
                    return;
                }

                var rect = _image.rectTransform;
                rect.sizeDelta = new Vector2(NativeWidth, NativeHeight);
                rect.localScale = new Vector3(_alignRight ? -NativeScale : NativeScale, NativeScale, NativeScale);
                if (_edgeOffsetX <= 0f)
                {
                    _edgeOffsetX = NativeOffsetX;
                }

                ApplyCurrentOffset();
            }

            private void ApplyCurrentOffset()
            {
                if (_image == null)
                {
                    return;
                }

                _image.rectTransform.anchoredPosition = new Vector2(_alignRight ? -_edgeOffsetX : _edgeOffsetX, 0f);
            }
        }

        private sealed class SettingsPageButtonStyle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
        {
            public Button Button { get; set; } = null!;

            public Text LabelText { get; set; } = null!;

            public SettingsPageButtonOrnament LeftArrow { get; set; } = null!;

            public SettingsPageButtonOrnament RightArrow { get; set; } = null!;

            public VoiceThemeButtonKind Kind { get; set; }

            private VoiceSettingsTheme? _theme;
            private bool _isHovered;
            private bool _isSelected;
            private bool _lastInteractable;
            private bool _ornamentsVisible;
            private float _lastButtonWidth = -1f;
            private float _lastLabelPreferredWidth = -1f;
            private string _lastLabelText = string.Empty;

            private const float OrnamentTargetGap = 10f;
            private const float OrnamentMaxInsideOffset = 30f;
            private const float OrnamentTextWidthPadding = 12f;

            public void ApplyTheme(VoiceSettingsTheme theme)
            {
                _theme = theme;

                if (Button != null)
                {
                    var buttonImage = Button.targetGraphic as Image;
                    if (buttonImage != null)
                    {
                        buttonImage.sprite = null;
                        buttonImage.type = Image.Type.Simple;
                        buttonImage.color = Color.clear;
                        buttonImage.preserveAspect = false;
                    }
                }

                if (LabelText != null)
                {
                    LabelText.font = theme.PrimaryFont;
                }

                ApplyOrnamentTheme(LeftArrow, theme);
                ApplyOrnamentTheme(RightArrow, theme);
                RefreshOrnamentLayout(force: true);
                _lastInteractable = IsButtonInteractable();
                _ornamentsVisible = false;
                _isSelected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
                RefreshVisualState();
            }

            private void Update()
            {
                RefreshOrnamentLayout();

                var isInteractable = IsButtonInteractable();
                if (isInteractable == _lastInteractable)
                {
                    return;
                }

                _lastInteractable = isInteractable;
                RefreshVisualState();
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                _isHovered = true;
                RefreshVisualState();
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                _isHovered = false;
                _isSelected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
                RefreshVisualState();
            }

            public void OnSelect(BaseEventData eventData)
            {
                _isSelected = true;
                RefreshVisualState();
            }

            public void OnDeselect(BaseEventData eventData)
            {
                _isSelected = false;
                RefreshVisualState();
            }

            private void OnEnable()
            {
                _isSelected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
                RefreshVisualState();
            }

            private void OnDisable()
            {
                _isHovered = false;
                _isSelected = false;
                RefreshVisualState();
            }

            private void ApplyOrnamentTheme(SettingsPageButtonOrnament ornament, VoiceSettingsTheme theme)
            {
                if (ornament == null)
                {
                    return;
                }

                ornament.ApplyTheme(theme, Kind);
                ornament.SetVisible(false);
            }

            private bool IsButtonInteractable()
            {
                return Button != null && Button.IsInteractable();
            }

            private void RefreshVisualState()
            {
                var isInteractable = IsButtonInteractable();
                _lastInteractable = isInteractable;
                RefreshOrnamentLayout();

                if (LabelText != null && _theme != null)
                {
                    LabelText.color = isInteractable ? _theme.TextColor : _theme.PlaceholderTextColor;
                }

                var showOrnaments = isInteractable && (_isHovered || _isSelected);
                if (showOrnaments == _ornamentsVisible)
                {
                    return;
                }

                _ornamentsVisible = showOrnaments;

                if (LeftArrow != null)
                {
                    LeftArrow.SetVisible(showOrnaments);
                }

                if (RightArrow != null)
                {
                    RightArrow.SetVisible(showOrnaments);
                }
            }

            private void RefreshOrnamentLayout(bool force = false)
            {
                if (Button == null || LabelText == null || LeftArrow == null || RightArrow == null)
                {
                    return;
                }

                var buttonRect = Button.GetComponent<RectTransform>();
                if (buttonRect == null)
                {
                    return;
                }

                var buttonWidth = buttonRect.rect.width;
                var labelText = LabelText.text ?? string.Empty;
                var labelPreferredWidth = CalculateEffectiveLabelWidth(buttonWidth);
                if (!force
                    && Mathf.Abs(buttonWidth - _lastButtonWidth) < 0.5f
                    && Mathf.Abs(labelPreferredWidth - _lastLabelPreferredWidth) < 0.5f
                    && string.Equals(labelText, _lastLabelText, StringComparison.Ordinal))
                {
                    return;
                }

                _lastButtonWidth = buttonWidth;
                _lastLabelPreferredWidth = labelPreferredWidth;
                _lastLabelText = labelText;

                var ornamentHalfWidth = Mathf.Max(LeftArrow.GetVisualHalfWidth(), RightArrow.GetVisualHalfWidth());
                var edgeOffset = buttonWidth * 0.5f - labelPreferredWidth * 0.5f - OrnamentTargetGap - ornamentHalfWidth;
                edgeOffset = Mathf.Min(edgeOffset, OrnamentMaxInsideOffset);

                LeftArrow.SetEdgeOffset(edgeOffset);
                RightArrow.SetEdgeOffset(edgeOffset);
            }

            private float CalculateEffectiveLabelWidth(float buttonWidth)
            {
                if (LabelText == null)
                {
                    return 0f;
                }

                var availableWidth = Mathf.Max(0f, buttonWidth - OrnamentTextWidthPadding * 2f);
                return Mathf.Min(LabelText.preferredWidth, availableWidth);
            }
        }
    }
}
