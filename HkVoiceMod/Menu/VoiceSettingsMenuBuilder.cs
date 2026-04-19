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

        internal static VoiceMacroStep CreateActionStep(global::GlobalEnums.HeroActionButton actionButton, VoiceModSettings settings)
        {
            switch (actionButton)
            {
                case global::GlobalEnums.HeroActionButton.LEFT:
                case global::GlobalEnums.HeroActionButton.RIGHT:
                    return VoiceMacroStep.CreateAction(new[] { actionButton }, KeyPressMode.ContinuousHold, 0f, true);
                case global::GlobalEnums.HeroActionButton.UP:
                case global::GlobalEnums.HeroActionButton.DOWN:
                case global::GlobalEnums.HeroActionButton.JUMP:
                case global::GlobalEnums.HeroActionButton.SUPER_DASH:
                case global::GlobalEnums.HeroActionButton.DREAM_NAIL:
                case global::GlobalEnums.HeroActionButton.QUICK_MAP:
                    return VoiceMacroStep.CreateAction(new[] { actionButton }, KeyPressMode.TimedHold, settings.TimedHoldDurationSeconds, false);
                case global::GlobalEnums.HeroActionButton.ATTACK:
                case global::GlobalEnums.HeroActionButton.DASH:
                case global::GlobalEnums.HeroActionButton.CAST:
                case global::GlobalEnums.HeroActionButton.QUICK_CAST:
                case global::GlobalEnums.HeroActionButton.INVENTORY:
                    return VoiceMacroStep.CreateAction(new[] { actionButton }, KeyPressMode.Tap, settings.ShortPressDurationSeconds, false);
                default:
                    throw new ArgumentOutOfRangeException(nameof(actionButton), actionButton, "Unsupported macro action button.");
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

            return string.Join(",", parts.ToArray());
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
                return $"(delay {Math.Round(step.DelaySeconds * 1000f, MidpointRounding.AwayFromZero)}ms)";
            }

            var actionButtons = step.GetNormalizedActionButtons();
            if (actionButtons.Count == 0)
            {
                return "(<无效动作>)";
            }

            var keyNames = new List<string>(actionButtons.Count);
            foreach (var actionButton in actionButtons)
            {
                keyNames.Add(resolver.GetDisplayName(actionButton));
            }

            return $"({string.Join("+", keyNames.ToArray())})";
        }
    }
}
