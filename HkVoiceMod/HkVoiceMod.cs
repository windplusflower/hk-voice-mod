using System;
using System.IO;
using HkVoiceMod.Commands;
using HkVoiceMod.Menu;
using HkVoiceMod.Recognition.Sherpa;
using HkVoiceMod.Recognition.Templates;
using Modding;
using UnityEngine;
using HkVoiceMod.Runtime;

namespace HkVoiceMod
{
    public sealed class HkVoiceMod : Mod, IGlobalSettings<VoiceModSettings>, ICustomMenuMod
    {
        private VoiceRuntimeController? _runtimeController;

        public HkVoiceMod() : base(nameof(HkVoiceMod))
        {
            Instance = this;
        }

        internal static HkVoiceMod? Instance { get; private set; }

        internal VoiceModSettings Settings { get; private set; } = new VoiceModSettings();

        public bool ToggleButtonInsideMenu => false;

        public override string GetVersion()
        {
            return "1.0";
        }

        public override void Initialize()
        {
            PrepareSettingsForRuntime(Settings, true);
            EnsureRuntimeController();
            LogInfo("Runtime initialized.");
        }

        public void OnLoadGlobal(VoiceModSettings settings)
        {
            Settings = settings?.Clone() ?? new VoiceModSettings();
            if (Settings.RequiresResetToEventStreamDefaults())
            {
                LogWarn("检测到旧版本地宏配置，已重置为新的事件流默认配置。");
                Settings.ResetToEventStreamDefaults();
            }

            PrepareSettingsForRuntime(Settings, true);
            _runtimeController?.ApplySettings(Settings);
        }

        public VoiceModSettings OnSaveGlobal()
        {
            return Settings.Clone();
        }

        public MenuScreen GetMenuScreen(MenuScreen modListMenu, ModToggleDelegates? toggleDelegates)
        {
            return new VoiceSettingsMenuBuilder().Build(modListMenu, this, toggleDelegates);
        }

        internal ApplyVoiceSettingsResult TryApplyVoiceCommandSettings(VoiceModSettings draftSettings)
        {
            if (draftSettings == null)
            {
                return ApplyVoiceSettingsResult.CreateFailure("Apply 失败：菜单草稿为空。");
            }

            var candidate = draftSettings.Clone();
            try
            {
                PrepareSettingsForRuntime(candidate, false);
                var assemblyDirectory = Path.GetDirectoryName(GetType().Assembly.Location) ?? AppDomain.CurrentDomain.BaseDirectory;
                candidate.CleanupTemplateFiles(assemblyDirectory);
                Settings = candidate;
                _runtimeController?.ApplySettings(Settings);
                return ApplyVoiceSettingsResult.CreateSuccess("已应用新的宏、停止词与阈值配置，并重启语音识别后端。");
            }
            catch (Exception ex)
            {
                return ApplyVoiceSettingsResult.CreateFailure($"Apply 失败：{ex.Message}");
            }
        }

        internal new void LogDebug(string message)
        {
            if (Settings.EnableVerboseLogging)
            {
                Log($"[DEBUG] {message}");
            }
        }

        internal void LogInfo(string message)
        {
            Log(message);
        }

        internal new void LogWarn(string message)
        {
            Log($"[WARN] {message}");
        }

        internal new void LogError(string message)
        {
            Log($"[ERROR] {message}");
        }

        internal void SuspendVoiceBackendForExclusiveCapture()
        {
            _runtimeController?.SuspendBackend();
        }

        internal void ResumeVoiceBackendAfterExclusiveCapture()
        {
            _runtimeController?.ResumeBackend();
        }

        private void EnsureRuntimeController()
        {
            if (_runtimeController != null)
            {
                _runtimeController.ApplySettings(Settings);
                return;
            }

            var gameObject = new GameObject("HkVoiceMod.Runtime");
            UnityEngine.Object.DontDestroyOnLoad(gameObject);

            _runtimeController = gameObject.AddComponent<VoiceRuntimeController>();
            _runtimeController.Initialize(this, Settings);
        }

        private void PrepareSettingsForRuntime(VoiceModSettings settings, bool fallbackToDefaults)
        {
            try
            {
                PrepareSettingsForRuntimeCore(settings);
            }
            catch (Exception ex)
            {
                if (!fallbackToDefaults)
                {
                    throw;
                }

                LogWarn($"检测到无效的语音宏设置，已回退到默认配置：{ex.Message}");
                settings.ResetToEventStreamDefaults();
                PrepareSettingsForRuntimeCore(settings);
            }
        }

        private void PrepareSettingsForRuntimeCore(VoiceModSettings settings)
        {
            if (settings.RequiresResetToEventStreamDefaults())
            {
                settings.ResetToEventStreamDefaults();
            }

            settings.EnsureMacroDefaults();
            settings.NormalizeAndValidateMacroSettings();
            settings.NormalizeRecognitionRuntimeSettings();

            var assemblyDirectory = Path.GetDirectoryName(GetType().Assembly.Location) ?? AppDomain.CurrentDomain.BaseDirectory;
            var compiler = new SherpaKeywordCompiler(new ManagedPinyinProvider());
            compiler.Compile(settings.ResolveModelPath(assemblyDirectory), settings.StopKeywordConfig, settings.GetOrderedMacroConfigs());
            settings.CleanupTemplateFiles(assemblyDirectory);
        }
    }
}
