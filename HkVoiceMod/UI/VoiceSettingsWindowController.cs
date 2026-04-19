using System;
using System.Collections.Generic;
using System.Globalization;
using HkVoiceMod.Commands;
using HkVoiceMod.Menu;
using Modding;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

        private static readonly Color FullscreenDimColor = new Color(0.03f, 0.04f, 0.08f, 0.82f);
        private static readonly Color WindowColor = new Color(0.10f, 0.12f, 0.16f, 0.97f);
        private static readonly Color SectionColor = new Color(0.14f, 0.17f, 0.22f, 0.98f);
        private static readonly Color RowColor = new Color(0.16f, 0.19f, 0.25f, 1f);
        private static readonly Color ModalColor = new Color(0.09f, 0.11f, 0.16f, 0.99f);
        private static readonly Color InputColor = new Color(0.07f, 0.08f, 0.11f, 1f);
        private static readonly Color PrimaryButtonColor = new Color(0.22f, 0.52f, 0.78f, 0.98f);
        private static readonly Color SecondaryButtonColor = new Color(0.24f, 0.28f, 0.36f, 0.98f);
        private static readonly Color DangerButtonColor = new Color(0.66f, 0.24f, 0.27f, 0.98f);
        private static readonly Color TextColor = new Color(0.95f, 0.96f, 0.98f, 1f);
        private static readonly Color MutedTextColor = new Color(0.72f, 0.76f, 0.82f, 1f);
        private static readonly Color PlaceholderTextColor = new Color(0.55f, 0.60f, 0.68f, 0.95f);
        private static readonly Color SuccessTextColor = new Color(0.67f, 0.89f, 0.73f, 1f);
        private static readonly Color ErrorTextColor = new Color(0.97f, 0.63f, 0.63f, 1f);

        private static VoiceSettingsWindowController? _instance;

        private readonly List<MacroRowWidgets> _macroRows = new List<MacroRowWidgets>();
        private Font? _font;
        private HkVoiceMod? _mod;
        private VoiceSettingsDraft? _draft;
        private Canvas? _canvas;
        private CanvasGroup? _canvasGroup;
        private RectTransform? _macroListContent;
        private GameObject? _modalHost;
        private GameObject? _recordingModal;
        private GameObject? _delayModal;
        private Text? _statusText;
        private InputField? _stopWakeWordInput;
        private InputField? _stopThresholdInput;
        private InputField? _delayInputField;
        private Text? _recordingTitleText;
        private Text? _recordingHintText;
        private Text? _recordingPreviewText;
        private Text? _recordingStatusText;
        private Text? _delayHintText;
        private Button? _recordingStartButton;
        private Button? _recordingStopButton;
        private Button? _recordingClearButton;
        private VoiceMacroConfig? _recordingMacro;
        private VoiceMacroConfig? _delayMacro;
        private List<VoiceMacroStep>? _recordingStartSnapshot;
        private int _delayModalBlockedFrame = -1;
        private string _stopThresholdText = string.Empty;
        private bool _isClosingWindow;

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
            _draft = VoiceSettingsDraft.FromAppliedSettings(_mod.Settings);
            _stopThresholdText = FormatThresholdText(_draft.PendingStopKeywordConfig.KeywordThreshold);
            _recordingMacro = null;
            _delayMacro = null;
            _recordingStartSnapshot = null;
            _delayModalBlockedFrame = -1;
            _isClosingWindow = false;

            EnsureBuilt();
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
            SetStatus("打开录制页后默认不会立即采集；点击“开始录制”后才会追加步骤。", false);
            SetVisible(true);
            FocusInputField(_stopWakeWordInput);
        }

        internal void ToggleFromMenu(HkVoiceMod mod, MenuScreen returnScreen)
        {
            if (IsVisible())
            {
                RequestBack();
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

            if (_delayModal != null && _delayModal.activeSelf)
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

            if (_recordingModal != null && _recordingModal.activeSelf)
            {
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) && !IsEditingTextInput())
            {
                RequestBack();
            }
        }

        private void OnDestroy()
        {
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

            _font = ResolveFont();

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

            var root = CreatePanel(gameObject.transform, "Root", FullscreenDimColor);
            StretchToParent(root);

            var window = CreatePanel(root.transform, "Window", WindowColor);
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

            var header = CreateSection(window.transform, "Header", 128f);
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

            var stopSection = CreateSection(window.transform, "StopSection", 112f);
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

            var footer = CreateSection(window.transform, "Footer", 74f);
            var footerLayout = CreateHorizontalLayout(footer.transform, 10f, new RectOffset(12, 12, 12, 12));
            footerLayout.childAlignment = TextAnchor.MiddleRight;

            CreateButton(footer.transform, "AddMacroButton", "新增语音宏", SecondaryButtonWidth, SecondaryButtonColor, AddMacro, out _);
            CreateSpacer(footer.transform, "FooterSpacer", 1f);
            CreateButton(footer.transform, "ApplyButton", "保存", PrimaryButtonWidth, PrimaryButtonColor, ApplyCurrentDraft, out _);
            CreateButton(footer.transform, "DiscardButton", "放弃更改", DiscardButtonWidth, DangerButtonColor, DiscardAndClose, out _);
            CreateButton(footer.transform, "BackButton", "返回", PrimaryButtonWidth, SecondaryButtonColor, RequestBack, out _);

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
            _recordingHintText = CreateText(modal.transform, "RecordingHint", "进入录制页后请先点击“开始录制”；停止录制时可使用 Backspace / Enter / Esc。", 24, MutedTextColor, FontStyle.Normal, TextAnchor.UpperLeft, TextAnchor.MiddleLeft, -1f);
            ConfigureWrappedAutoHeightText(_recordingHintText, 34f);

            var previewPanel = CreatePanel(modal.transform, "RecordingPreviewPanel", SectionColor);
            AddLayoutElement(previewPanel, -1f, 220f, 1f);
            var previewLayout = previewPanel.AddComponent<VerticalLayoutGroup>();
            previewLayout.padding = new RectOffset(16, 16, 16, 16);
            previewLayout.spacing = 10f;
            previewLayout.childControlWidth = true;
            previewLayout.childControlHeight = true;
            previewLayout.childForceExpandWidth = true;
            previewLayout.childForceExpandHeight = false;

            CreateText(previewPanel.transform, "RecordingPreviewLabel", "当前完整步骤序列", 24, TextColor, FontStyle.Bold, TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 30f);
            var previewScroll = CreateScrollView(previewPanel.transform, "RecordingPreviewScroll", out var previewContent, 0f);
            AddLayoutElement(previewScroll, -1f, -1f, 1f);
            _recordingPreviewText = CreateText(previewContent, "RecordingPreviewText", string.Empty, 24, TextColor, FontStyle.Normal, TextAnchor.UpperLeft, TextAnchor.UpperLeft, -1f);
            ConfigureWrappedAutoHeightText(_recordingPreviewText);

            _recordingStatusText = CreateText(modal.transform, "RecordingStatus", string.Empty, 24, MutedTextColor, FontStyle.Normal, TextAnchor.UpperLeft, TextAnchor.MiddleLeft, -1f);
            ConfigureWrappedAutoHeightText(_recordingStatusText, 60f);

            var actionRow = CreateHorizontalGroup(modal.transform, "RecordingActions", 16f, 60f, new RectOffset(0, 0, 4, 0));
            actionRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;
            CreateSpacer(actionRow.transform, "RecordingActionSpacer", 1f);
            CreateButton(actionRow.transform, "RecordingStart", "开始录制", SecondaryButtonWidth, PrimaryButtonColor, StartRecordingFromButton, out _recordingStartButton, out _);
            CreateButton(actionRow.transform, "RecordingStop", "停止录制", SecondaryButtonWidth, SecondaryButtonColor, StopRecordingFromButton, out _recordingStopButton, out _);
            CreateButton(actionRow.transform, "RecordingClear", "清空", SecondaryButtonWidth, DangerButtonColor, ClearRecordingFromButton, out _recordingClearButton, out _);
            CreateButton(actionRow.transform, "RecordingDelay", "插入延迟", SecondaryButtonWidth, SecondaryButtonColor, OpenDelayModal, out _);
            CreateButton(actionRow.transform, "RecordingConfirm", "确认", SecondaryButtonWidth, PrimaryButtonColor, ConfirmRecordingFromButton, out _);
            CreateButton(actionRow.transform, "RecordingCancel", "取消", SecondaryButtonWidth, SecondaryButtonColor, CancelRecordingFromButton, out _);

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

            CreateText(modal.transform, "DelayTitle", "添加延迟", 32, TextColor, FontStyle.Bold, TextAnchor.MiddleLeft, TextAnchor.MiddleLeft, 40f);
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

            CreateButton(topRow.transform, $"MacroRecord-{macro.Id}", "录制", SecondaryButtonWidth, PrimaryButtonColor, () => OpenRecordingModal(macro), out var recordButtonText);
            CreateButton(topRow.transform, $"MacroDelete-{macro.Id}", "删除", DeleteButtonWidth, DangerButtonColor, () => DeleteMacro(macro.Id), out _);

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

            SetStatus("保存失败：请检查空唤醒词、无效阈值或空步骤。", true);
        }

        private void RequestBack()
        {
            if (_isClosingWindow)
            {
                return;
            }

            if (_delayModal != null && _delayModal.activeSelf)
            {
                CancelDelayInput();
                return;
            }

            if (_recordingModal != null && _recordingModal.activeSelf)
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

            VoiceMacroCaptureService.Instance.StopCapture();
            _recordingMacro = macro;
            _recordingStartSnapshot = _draft.CloneMacroSteps(macro.Id);
            _recordingTitleText.text = $"录制宏：{VoiceSettingsMenuBuilder.GetMacroDisplayName(macro)}";

            _modalHost.SetActive(true);
            _recordingModal.SetActive(true);
            if (_delayModal != null)
            {
                _delayModal.SetActive(false);
            }

            VoiceMacroCaptureService.Instance.BeginCapture(
                macro.Id,
                actionButton => macro.Steps.Add(VoiceSettingsMenuBuilder.CreateActionStep(actionButton, _draft.CreateSettingsSnapshot())),
                () =>
                {
                    if (macro.Steps.Count > 0)
                    {
                        macro.Steps.RemoveAt(macro.Steps.Count - 1);
                    }
                },
                () => CloseRecordingModal(false, true),
                () => CloseRecordingModal(true, true));

            RefreshDynamicContent();
        }

        private void StartRecordingFromButton()
        {
            if (_recordingMacro == null)
            {
                return;
            }

            VoiceMacroCaptureService.Instance.StartCapture(_recordingMacro.Id);
            RefreshDynamicContent();
        }

        private void StopRecordingFromButton()
        {
            if (_recordingMacro == null)
            {
                return;
            }

            VoiceMacroCaptureService.Instance.StopActiveCapture(_recordingMacro.Id);
            RefreshDynamicContent();
        }

        private void ClearRecordingFromButton()
        {
            if (_recordingMacro == null)
            {
                return;
            }

            VoiceMacroCaptureService.Instance.StopActiveCapture(_recordingMacro.Id);
            _recordingMacro.Steps.Clear();
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
            if (_draft != null && _recordingMacro != null && !applyChanges && _recordingStartSnapshot != null)
            {
                _draft.ReplaceMacroSteps(_recordingMacro.Id, _recordingStartSnapshot);
            }

            if (stopCapture)
            {
                VoiceMacroCaptureService.Instance.StopCapture();
            }

            _recordingMacro = null;
            _recordingStartSnapshot = null;

            if (_delayModal != null)
            {
                _delayModal.SetActive(false);
            }

            if (_recordingModal != null)
            {
                _recordingModal.SetActive(false);
            }

            HideModalHostIfIdle();
            RefreshDynamicContent();
        }

        private void OpenDelayModal()
        {
            if (_draft == null || _recordingMacro == null || _delayModal == null || _recordingModal == null)
            {
                return;
            }

            _delayMacro = _recordingMacro;
            VoiceMacroCaptureService.Instance.SuspendCapture();
            var pendingDelay = _draft.GetPendingDelayMilliseconds(_delayMacro.Id);
            SetInputFieldText(_delayInputField, pendingDelay > 0 ? pendingDelay.ToString(CultureInfo.InvariantCulture) : string.Empty);
            _delayModalBlockedFrame = Time.frameCount;
            _recordingModal.SetActive(false);
            _delayModal.SetActive(true);
            RefreshDynamicContent();
            FocusInputField(_delayInputField);
        }

        private void ConfirmDelayInput()
        {
            if (_draft == null || _delayMacro == null || _delayInputField == null)
            {
                return;
            }

            var trimmed = (_delayInputField.text ?? string.Empty).Trim();
            if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds) || milliseconds <= 0)
            {
                SetStatus("延迟必须是大于 0 的整数毫秒。", true);
                return;
            }

            _draft.SetPendingDelayMilliseconds(_delayMacro.Id, milliseconds);
            VoiceSettingsMenuBuilder.AppendDelayStep(_draft, _delayMacro, milliseconds);
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
            VoiceMacroCaptureService.Instance.StopCapture();
            _recordingMacro = null;
            _delayMacro = null;
            _recordingStartSnapshot = null;
            _delayModalBlockedFrame = -1;

            if (_delayModal != null)
            {
                _delayModal.SetActive(false);
            }

            if (_recordingModal != null)
            {
                _recordingModal.SetActive(false);
            }

            HideModalHostIfIdle();
            SetVisible(false);
            SelectGameObject(null);
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

                if (_recordingHintText != null)
                {
                    _recordingHintText.text = isRecording
                        ? "正在录制：按游戏当前绑定键会直接加入步骤；如需删除、确认或取消，请先点击“停止录制”。"
                        : "当前未录制：点击“开始录制”后才会追加步骤；停止录制时可使用 Backspace / Enter / Esc。";
                }

                if (_recordingStartButton != null)
                {
                    _recordingStartButton.interactable = hasSession && !isRecording && !isSuspended;
                }

                if (_recordingStopButton != null)
                {
                    _recordingStopButton.interactable = hasSession && isRecording;
                }

                if (_recordingClearButton != null)
                {
                    _recordingClearButton.interactable = hasSession && !isRecording && _recordingMacro.Steps.Count > 0;
                }

                if (_recordingPreviewText != null)
                {
                    _recordingPreviewText.text = VoiceSettingsMenuBuilder.FormatMacroSteps(_recordingMacro, VoiceMacroCaptureService.Instance.Resolver);
                }

                if (_recordingStatusText != null)
                {
                    _recordingStatusText.text = VoiceMacroCaptureService.Instance.GetStatusText(_recordingMacro.Id);
                }
            }

            if (_draft != null && _delayMacro != null && _delayHintText != null)
            {
                _delayHintText.text = $"当前将插入：{_draft.GetPendingDelayMilliseconds(_delayMacro.Id)} 毫秒";
            }
        }

        private string BuildMacroSummaryText(VoiceMacroConfig macro)
        {
            var prefix = VoiceMacroCaptureService.Instance.IsCapturing(macro.Id) ? "录制中：" : "当前宏：";
            return prefix + VoiceSettingsMenuBuilder.FormatMacroSteps(macro, VoiceMacroCaptureService.Instance.Resolver);
        }

        private void SetStatus(string message, bool isError, bool isSuccess = false)
        {
            if (_statusText == null)
            {
                return;
            }

            _statusText.text = message ?? string.Empty;
            _statusText.color = isError ? ErrorTextColor : (isSuccess ? SuccessTextColor : MutedTextColor);
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

            var recordingVisible = _recordingModal != null && _recordingModal.activeSelf;
            var delayVisible = _delayModal != null && _delayModal.activeSelf;
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
            image.color = InputColor;
            image.type = Image.Type.Sliced;

            inputField = inputObject.GetComponent<InputField>();
            inputField.targetGraphic = image;
            inputField.contentType = contentType;
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.transition = Selectable.Transition.ColorTint;

            var colors = inputField.colors;
            colors.normalColor = InputColor;
            colors.highlightedColor = new Color(0.11f, 0.13f, 0.18f, 1f);
            colors.pressedColor = new Color(0.10f, 0.12f, 0.16f, 1f);
            colors.selectedColor = new Color(0.14f, 0.18f, 0.24f, 1f);
            colors.disabledColor = new Color(0.12f, 0.12f, 0.12f, 0.6f);
            inputField.colors = colors;

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(inputObject.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 8f);
            textRect.offsetMax = new Vector2(-12f, -8f);

            var text = textObject.GetComponent<Text>();
            text.font = _font;
            text.fontSize = 22;
            text.color = TextColor;
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
            placeholderText.font = _font;
            placeholderText.fontSize = 22;
            placeholderText.fontStyle = FontStyle.Italic;
            placeholderText.color = PlaceholderTextColor;
            placeholderText.alignment = TextAnchor.MiddleLeft;
            placeholderText.horizontalOverflow = HorizontalWrapMode.Overflow;
            placeholderText.verticalOverflow = VerticalWrapMode.Truncate;
            placeholderText.text = placeholder;
            placeholderText.raycastTarget = false;

            inputField.textComponent = text;
            inputField.placeholder = placeholderText;
            inputField.onValueChanged.AddListener(value => onValueChanged(value));
        }

        private void CreateButton(Transform parent, string name, string label, float width, Color backgroundColor, Action onClick, out Text labelText)
        {
            CreateButton(parent, name, label, width, backgroundColor, onClick, out _, out labelText);
        }

        private void CreateButton(Transform parent, string name, string label, float width, Color backgroundColor, Action onClick, out Button button, out Text labelText)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            AddLayoutElement(buttonObject, width, FieldHeight, 0f);

            var image = buttonObject.GetComponent<Image>();
            image.color = backgroundColor;
            image.type = Image.Type.Sliced;

            button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;

            var colors = button.colors;
            colors.normalColor = backgroundColor;
            colors.highlightedColor = Color.Lerp(backgroundColor, Color.white, 0.12f);
            colors.pressedColor = Color.Lerp(backgroundColor, Color.black, 0.18f);
            colors.selectedColor = Color.Lerp(backgroundColor, Color.white, 0.08f);
            colors.disabledColor = new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, 0.5f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick());

            labelText = CreateText(buttonObject.transform, $"{name}Label", label, 22, TextColor, FontStyle.Bold, TextAnchor.MiddleCenter, TextAnchor.MiddleCenter, FieldHeight);
            StretchToParent(labelText.gameObject);
            var labelRect = labelText.GetComponent<RectTransform>();
            labelRect.offsetMin = new Vector2(10f, 6f);
            labelRect.offsetMax = new Vector2(-10f, -6f);
            labelText.horizontalOverflow = HorizontalWrapMode.Wrap;
            labelText.verticalOverflow = VerticalWrapMode.Truncate;
            labelText.resizeTextForBestFit = true;
            labelText.resizeTextMinSize = 18;
            labelText.resizeTextMaxSize = 22;
            labelText.raycastTarget = false;
        }

        private GameObject CreatePanel(Transform parent, string name, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var image = panel.GetComponent<Image>();
            image.color = color;
            image.type = Image.Type.Sliced;
            return panel;
        }

        private Text CreateText(Transform parent, string name, string textValue, int fontSize, Color color, FontStyle fontStyle, TextAnchor alignment, TextAnchor childAlignment, float preferredHeight, bool flexibleHeight = false, float preferredWidth = -1f)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            AddLayoutElement(textObject, preferredWidth, preferredHeight, flexibleHeight ? 1f : 0f);

            var text = textObject.GetComponent<Text>();
            text.font = _font;
            text.text = textValue;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
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
    }
}
