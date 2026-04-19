using System;
using System.Collections.Generic;
using HkVoiceMod.Commands;
using HkVoiceMod.UI;
using Modding;
using Satchel.BetterMenus;

namespace HkVoiceMod.Menu
{
    public sealed class VoiceSettingsMenuBuilder
    {
        private const float ThinMenuTextWidth = 1100f;

        public MenuScreen Build(MenuScreen modListMenu, HkVoiceMod mod, ModToggleDelegates? toggleDelegates)
        {
            if (mod == null)
            {
                throw new ArgumentNullException(nameof(mod));
            }

            MenuScreen? menuScreen = null;
            var titleText = new TextPanel("HKVOICEMOD 设置", ThinMenuTextWidth, 40, "hkvoicemod-settings-title");
            var descriptionText = new TextPanel(
                "这是官方支持的薄设置页。选择下面的按钮后，才会打开自制 Unity 编辑窗口；关闭编辑窗口后会自然回到这里。",
                ThinMenuTextWidth,
                28,
                "hkvoicemod-settings-description");
            var overlayButton = new MenuButton(
                "打开 / 关闭 自定义编辑器",
                "显式切换自制编辑窗口。宏编辑、录制弹窗和 Delay 输入都在该窗口内完成。",
                _ =>
                {
                    if (menuScreen != null)
                    {
                        VoiceSettingsWindowController.Instance.ToggleFromMenu(mod, menuScreen);
                    }
                },
                false,
                "hkvoicemod-toggle-overlay");

            var menu = new Satchel.BetterMenus.Menu("HkVoiceMod", new Element[] { titleText, descriptionText, overlayButton });
            menuScreen = menu.GetMenuScreen(modListMenu);
            menu.CancelAction = () =>
            {
                if (VoiceSettingsWindowController.Instance.TryHandleMenuCancel())
                {
                    return;
                }

                Satchel.BetterMenus.Utils.GoToMenuScreen(menu.returnScreen);
            };

            return menuScreen;
        }

        internal static VoiceMacroConfig CreateNewMacro()
        {
            return new VoiceMacroConfig
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = string.Empty,
                WakeWord = string.Empty,
                KeywordThreshold = -1f,
                Steps = new List<VoiceMacroStep>(),
                IsPreset = false
            };
        }

        internal static VoiceMacroStep CreateActionStep(HeroActionKey heroActionKey, VoiceModSettings settings)
        {
            switch (heroActionKey)
            {
                case HeroActionKey.Left:
                case HeroActionKey.Right:
                    return VoiceMacroStep.CreateAction(new[] { heroActionKey }, KeyPressMode.ContinuousHold, 0f, true);
                case HeroActionKey.Up:
                case HeroActionKey.Down:
                case HeroActionKey.Jump:
                    return VoiceMacroStep.CreateAction(new[] { heroActionKey }, KeyPressMode.TimedHold, settings.TimedHoldDurationSeconds, false);
                case HeroActionKey.Attack:
                case HeroActionKey.Dash:
                case HeroActionKey.Cast:
                    return VoiceMacroStep.CreateAction(new[] { heroActionKey }, KeyPressMode.Tap, settings.ShortPressDurationSeconds, false);
                default:
                    throw new ArgumentOutOfRangeException(nameof(heroActionKey), heroActionKey, "Unsupported macro action key.");
            }
        }

        internal static void AppendDelayStep(VoiceSettingsDraft draft, VoiceMacroConfig macro, int delayMilliseconds)
        {
            if (delayMilliseconds <= 0)
            {
                return;
            }

            draft.SetPendingDelayMilliseconds(macro.Id, delayMilliseconds);
            var steps = draft.CloneMacroSteps(macro.Id);
            steps.Add(VoiceMacroStep.CreateDelay(delayMilliseconds / 1000f));
            draft.ReplaceMacroSteps(macro.Id, steps);
        }

        internal static bool TryApplyDraft(HkVoiceMod mod, VoiceSettingsDraft draft)
        {
            PrepareDraftForApply(draft);

            var result = mod.TryApplyVoiceCommandSettings(draft.CreateSettingsSnapshot());
            if (result.Success)
            {
                mod.LogInfo(result.Message);
                return true;
            }

            mod.LogWarn(result.Message);
            return false;
        }

        internal static string FormatMacroSteps(VoiceMacroConfig macro, GameKeybindNameResolver resolver)
        {
            if (macro.Steps == null || macro.Steps.Count == 0)
            {
                return "<空>";
            }

            var parts = new List<string>(macro.Steps.Count);
            foreach (var step in macro.Steps)
            {
                parts.Add(FormatMacroStep(step, resolver));
            }

            return string.Join(" ", parts.ToArray());
        }

        internal static string GetMacroDisplayName(VoiceMacroConfig macro)
        {
            var normalizedWakeWord = VoiceModSettings.NormalizeWakeWord(macro.WakeWord);
            if (normalizedWakeWord.Length > 0)
            {
                return normalizedWakeWord;
            }

            return string.IsNullOrWhiteSpace(macro.DisplayName) ? "未命名宏" : macro.DisplayName;
        }

        private static void PrepareDraftForApply(VoiceSettingsDraft draft)
        {
            foreach (var macro in draft.PendingMacroConfigs)
            {
                PrepareMacroForApply(macro);
            }
        }

        private static void PrepareMacroForApply(VoiceMacroConfig macro)
        {
            var normalizedWakeWord = VoiceModSettings.NormalizeWakeWord(macro.WakeWord);
            macro.DisplayName = normalizedWakeWord.Length > 0 ? normalizedWakeWord : string.Empty;
        }

        private static string FormatMacroStep(VoiceMacroStep step, GameKeybindNameResolver resolver)
        {
            if (step.StepKind == VoiceMacroStepKind.Delay)
            {
                return $"Delay {Math.Round(step.DelaySeconds * 1000f, MidpointRounding.AwayFromZero)}ms";
            }

            var keyNames = new List<string>(step.Keys?.Count ?? 0);
            if (step.Keys != null)
            {
                foreach (var key in step.Keys)
                {
                    keyNames.Add(resolver.GetDisplayName(key));
                }
            }

            return string.Join("+", keyNames.ToArray());
        }
    }
}
