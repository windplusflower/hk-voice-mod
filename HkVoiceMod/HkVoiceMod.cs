using System;
using Modding;
using UnityEngine;
using HkVoiceMod.Runtime;

namespace HkVoiceMod
{
    public sealed class HkVoiceMod : Mod, IGlobalSettings<VoiceModSettings>
    {
        private VoiceRuntimeController? _runtimeController;

        public HkVoiceMod() : base(nameof(HkVoiceMod))
        {
            Instance = this;
        }

        internal static HkVoiceMod? Instance { get; private set; }

        internal VoiceModSettings Settings { get; private set; } = new VoiceModSettings();

        public override string GetVersion()
        {
            return "0.1.0";
        }

        public override void Initialize()
        {
            EnsureRuntimeController();
            LogInfo("Runtime initialized.");
        }

        public void OnLoadGlobal(VoiceModSettings settings)
        {
            Settings = settings?.Clone() ?? new VoiceModSettings();
            _runtimeController?.ApplySettings(Settings);
        }

        public VoiceModSettings OnSaveGlobal()
        {
            return Settings.Clone();
        }

        internal void LogDebug(string message)
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

        internal void LogWarn(string message)
        {
            Log($"[WARN] {message}");
        }

        internal void LogError(string message)
        {
            Log($"[ERROR] {message}");
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
    }
}
