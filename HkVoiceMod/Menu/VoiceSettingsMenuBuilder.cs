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
                new TextPanel("自定义每个操作的唤醒词与阈值", 1000f),
                new TextPanel("如果识别不出来，可以把命令增加到 3~4 个字，或适当调低阈值。", 1000f),
                new TextPanel("修改后只有点击 Apply 才会保存并重启识别后端", 1000f)
            };

            foreach (var config in draft.PendingCommandKeywordConfigs)
            {
                var definition = VoiceCommandCatalog.GetDefinition(config.Command);
                elements.Add(new TextPanel($"{definition.DisplayName}（{config.Command}）", 1000f));
                elements.Add(new InputField(
                    $"唤醒词：{definition.DisplayName}",
                    value => config.WakeWord = value ?? string.Empty,
                    () => config.WakeWord ?? string.Empty,
                    definition.DefaultWakeWord,
                    12));
                elements.Add(Blueprints.FloatInputField(
                    $"阈值：{definition.DisplayName}",
                    value => config.KeywordThreshold = value,
                    () => config.KeywordThreshold,
                    definition.DefaultThreshold,
                    "0.01-1.00",
                    6));
            }

            elements.Add(new MenuButton(
                "Apply",
                "保存配置并重启语音识别后端",
                _ => applyAction()));

            var menu = new Satchel.BetterMenus.Menu("HkVoiceMod", elements.ToArray());
            var menuScreen = menu.GetMenuScreen(modListMenu);
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
                            Satchel.BetterMenus.Utils.GoToMenuScreen(menu.returnScreen);
                            return;
                        }

                        mod.LogWarn(result.Message);
                        Satchel.BetterMenus.Utils.GoToMenuScreen(menu.menuScreen);
                        return;
                    }

                    if (choice == "直接返回")
                    {
                        Satchel.BetterMenus.Utils.GoToMenuScreen(menu.returnScreen);
                        return;
                    }

                    Satchel.BetterMenus.Utils.GoToMenuScreen(menu.menuScreen);
                });

            menu.CancelAction = () =>
            {
                if (draft.HasPendingChanges(mod.Settings))
                {
                    menu.ShowDialog(confirmMenu);
                    return;
                }

                Satchel.BetterMenus.Utils.GoToMenuScreen(menu.returnScreen);
            };

            return menuScreen;
        }
    }
}
