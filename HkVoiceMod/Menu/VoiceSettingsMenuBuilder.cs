using System;
using System.Collections.Generic;
using HkVoiceMod.Commands;
using Modding;
using Satchel.BetterMenus;

namespace HkVoiceMod.Menu
{
    public sealed class VoiceSettingsMenuBuilder
    {
        public MenuScreen Build(MenuScreen modListMenu, HkVoiceMod mod, ModToggleDelegates? toggleDelegates)
        {
            if (mod == null)
            {
                throw new ArgumentNullException(nameof(mod));
            }

            var draft = VoiceSettingsDraft.FromAppliedSettings(mod.Settings);
            return BuildRootMenu(modListMenu, mod, toggleDelegates, draft, VoiceMacroCaptureService.Instance);
        }

        private MenuScreen BuildRootMenu(MenuScreen modListMenu, HkVoiceMod mod, ModToggleDelegates? toggleDelegates, VoiceSettingsDraft draft, VoiceMacroCaptureService captureService)
        {
            Action applyAction = () =>
            {
                var result = mod.TryApplyVoiceCommandSettings(draft.CreateSettingsSnapshot());
                if (result.Success)
                {
                    mod.LogInfo(result.Message);
                    return;
                }

                mod.LogWarn(result.Message);
            };

            var elements = new List<Element>
            {
                new TextPanel("自定义语音宏", 1000f),
                new TextPanel("Stop 固定在最上方，只能改唤醒词和阈值；其他宏允许新增与删除。", 1000f),
                new TextPanel("宏录制：开始录制后按游戏当前绑定键追加动作；Delay 按钮追加延迟。", 1000f),
                new TextPanel("修改后只有点击 Apply 才会保存并重启识别后端", 1000f),
                new TextPanel("Stop（固定项）", 1000f),
                new InputField(
                    "Stop 唤醒词",
                    value => draft.PendingStopKeywordConfig.WakeWord = value ?? string.Empty,
                    () => draft.PendingStopKeywordConfig.WakeWord ?? string.Empty,
                    "停止",
                    12),
                Blueprints.FloatInputField(
                    "Stop 阈值",
                    value => draft.PendingStopKeywordConfig.KeywordThreshold = value,
                    () => draft.PendingStopKeywordConfig.KeywordThreshold,
                    0.1f,
                    "0.01-1.00",
                    6),
                new MenuButton(
                    "Add Macro",
                    "新增一个空宏",
                    _ =>
                    {
                        draft.AddMacro(CreateNewMacro());
                        Satchel.BetterMenus.Utils.GoToMenuScreen(BuildRootMenu(modListMenu, mod, toggleDelegates, draft, captureService));
                    })
            };

            MenuScreen? menuScreen = null;
            foreach (var macro in draft.PendingMacroConfigs)
            {
                elements.Add(new TextPanel($"{macro.DisplayName}（{macro.WakeWord}）", 1000f));
                elements.Add(new TextPanel($"阈值：{macro.KeywordThreshold:0.##}", 1000f));
                elements.Add(new TextPanel($"步骤：{FormatMacroSteps(macro, captureService.Resolver)}", 1000f));
                elements.Add(new MenuButton(
                    $"Edit {macro.DisplayName}",
                    "编辑这个宏",
                    _ =>
                    {
                        if (menuScreen != null)
                        {
                            Satchel.BetterMenus.Utils.GoToMenuScreen(BuildMacroEditorMenu(modListMenu, menuScreen, mod, draft, macro.Id, captureService));
                        }
                    }));
                elements.Add(new MenuButton(
                    $"Delete {macro.DisplayName}",
                    "删除这个宏",
                    _ =>
                    {
                        captureService.StopCapture();
                        draft.RemoveMacro(macro.Id);
                        Satchel.BetterMenus.Utils.GoToMenuScreen(BuildRootMenu(modListMenu, mod, toggleDelegates, draft, captureService));
                    }));
            }

            elements.Add(new MenuButton(
                "Apply",
                "保存配置并重启语音识别后端",
                _ => applyAction()));

            var menu = new Satchel.BetterMenus.Menu("HkVoiceMod", elements.ToArray());
            menuScreen = menu.GetMenuScreen(modListMenu);
            var confirmMenu = Blueprints.CreateDialogMenu(
                "检测到未应用修改",
                "是否先 Apply 当前修改？",
                new[] { "Apply 并返回", "直接返回", "继续编辑" },
                choice =>
                {
                    if (choice == "Apply 并返回")
                    {
                        var result = mod.TryApplyVoiceCommandSettings(draft.CreateSettingsSnapshot());
                        if (result.Success)
                        {
                            mod.LogInfo(result.Message);
                            captureService.StopCapture();
                            Satchel.BetterMenus.Utils.GoToMenuScreen(menu.returnScreen);
                            return;
                        }

                        mod.LogWarn(result.Message);
                        Satchel.BetterMenus.Utils.GoToMenuScreen(menu.menuScreen);
                        return;
                    }

                    if (choice == "直接返回")
                    {
                        captureService.StopCapture();
                        Satchel.BetterMenus.Utils.GoToMenuScreen(menu.returnScreen);
                        return;
                    }

                    Satchel.BetterMenus.Utils.GoToMenuScreen(menu.menuScreen);
                });

            menu.CancelAction = () =>
            {
                captureService.StopCapture();
                if (draft.HasPendingChanges(mod.Settings))
                {
                    menu.ShowDialog(confirmMenu);
                    return;
                }

                Satchel.BetterMenus.Utils.GoToMenuScreen(menu.returnScreen);
            };

            return menuScreen;
        }

        private MenuScreen BuildMacroEditorMenu(MenuScreen modListMenu, MenuScreen returnScreen, HkVoiceMod mod, VoiceSettingsDraft draft, string macroId, VoiceMacroCaptureService captureService)
        {
            var macro = draft.FindMacro(macroId);
            if (macro == null)
            {
                return BuildRootMenu(modListMenu, mod, null, draft, captureService);
            }

            var elements = new List<Element>
            {
                new TextPanel($"编辑宏：{macro.DisplayName}", 1000f),
                new InputField(
                    "显示名",
                    value => macro.DisplayName = value ?? string.Empty,
                    () => macro.DisplayName ?? string.Empty,
                    macro.DisplayName,
                    16),
                new InputField(
                    "唤醒词",
                    value => macro.WakeWord = value ?? string.Empty,
                    () => macro.WakeWord ?? string.Empty,
                    macro.WakeWord,
                    12),
                Blueprints.FloatInputField(
                    "阈值",
                    value => macro.KeywordThreshold = value,
                    () => macro.KeywordThreshold,
                    0.1f,
                    "0.01-1.00",
                    6),
                new InputField(
                    "步骤摘要（只读）",
                    _ => { },
                    () => FormatMacroSteps(macro, captureService.Resolver),
                    string.Empty,
                    48),
                new InputField(
                    "录制状态（只读）",
                    _ => { },
                    () => captureService.GetStatusText(macro.Id),
                    string.Empty,
                    64),
                new MenuButton(
                    "开始录制",
                    "进入键位录制模式",
                    _ => BeginCapture(captureService, draft, macro)),
                new MenuButton(
                    "结束录制",
                    "结束当前键位录制模式",
                    _ => captureService.StopCapture()),
                Blueprints.FloatInputField(
                    "待添加延迟（秒）",
                    value => draft.SetPendingDelaySeconds(macro.Id, value),
                    () => draft.GetPendingDelaySeconds(macro.Id),
                    0.5f,
                    ">0",
                    6),
                new MenuButton(
                    "Add Delay",
                    "把上面的延迟加入步骤列表",
                    _ =>
                    {
                        var delaySeconds = draft.GetPendingDelaySeconds(macro.Id);
                        if (delaySeconds > 0f)
                        {
                            macro.Steps.Add(VoiceMacroStep.CreateDelay(delaySeconds));
                        }
                    }),
                new MenuButton(
                    "删除末尾步骤",
                    "删除最后一个动作或延迟",
                    _ => RemoveLastStep(macro)),
                new MenuButton(
                    "清空步骤",
                    "清空整个宏的步骤列表",
                    _ => macro.Steps.Clear()),
                new MenuButton(
                    "删除这个宏",
                    "删除并返回宏列表",
                    _ =>
                    {
                        captureService.StopCapture();
                        draft.RemoveMacro(macro.Id);
                        Satchel.BetterMenus.Utils.GoToMenuScreen(BuildRootMenu(modListMenu, mod, null, draft, captureService));
                    }),
                new MenuButton(
                    "返回宏列表",
                    "返回上一级",
                    _ =>
                    {
                        captureService.StopCapture();
                        Satchel.BetterMenus.Utils.GoToMenuScreen(BuildRootMenu(modListMenu, mod, null, draft, captureService));
                    })
            };

            var menu = new Satchel.BetterMenus.Menu($"Macro-{macro.DisplayName}", elements.ToArray());
            var menuScreen = menu.GetMenuScreen(returnScreen);
            menu.CancelAction = () =>
            {
                captureService.StopCapture();
                Satchel.BetterMenus.Utils.GoToMenuScreen(BuildRootMenu(modListMenu, mod, null, draft, captureService));
            };

            return menuScreen;
        }

        private static VoiceMacroConfig CreateNewMacro()
        {
            return new VoiceMacroConfig
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = "新宏",
                WakeWord = string.Empty,
                KeywordThreshold = 0.1f,
                Steps = new List<VoiceMacroStep>(),
                IsPreset = false
            };
        }

        private static void BeginCapture(VoiceMacroCaptureService captureService, VoiceSettingsDraft draft, VoiceMacroConfig macro)
        {
            var snapshot = CloneSteps(macro.Steps);
            captureService.BeginCapture(
                macro.Id,
                actionKey => macro.Steps.Add(CreateActionStep(actionKey, draft.CreateSettingsSnapshot())),
                () => RemoveLastStep(macro),
                () => macro.Steps = CloneSteps(snapshot),
                () => { });
        }

        private static VoiceMacroStep CreateActionStep(HeroActionKey heroActionKey, VoiceModSettings settings)
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

        private static void RemoveLastStep(VoiceMacroConfig macro)
        {
            if (macro.Steps.Count > 0)
            {
                macro.Steps.RemoveAt(macro.Steps.Count - 1);
            }
        }

        private static List<VoiceMacroStep> CloneSteps(List<VoiceMacroStep> steps)
        {
            var clones = new List<VoiceMacroStep>(steps.Count);
            foreach (var step in steps)
            {
                if (step != null)
                {
                    clones.Add(step.Clone());
                }
            }

            return clones;
        }

        private static string FormatMacroSteps(VoiceMacroConfig macro, GameKeybindNameResolver resolver)
        {
            if (macro.Steps == null || macro.Steps.Count == 0)
            {
                return "<空>";
            }

            var parts = new List<string>(macro.Steps.Count);
            foreach (var step in macro.Steps)
            {
                if (step.StepKind == VoiceMacroStepKind.Delay)
                {
                    parts.Add(step.DelaySeconds.ToString("0.###"));
                    continue;
                }

                var keyNames = new List<string>(step.Keys.Count);
                foreach (var key in step.Keys)
                {
                    keyNames.Add(resolver.GetDisplayName(key));
                }

                parts.Add(string.Join("+", keyNames));
            }

            return string.Join(" ", parts.ToArray());
        }
    }
}
