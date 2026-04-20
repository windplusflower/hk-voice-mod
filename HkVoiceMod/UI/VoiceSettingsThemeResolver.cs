using System;
using Modding.Menu;
using UnityEngine;
using UnityEngine.UI;

namespace HkVoiceMod.UI
{
    internal static class VoiceSettingsThemeResolver
    {
        public static VoiceSettingsTheme Resolve(MenuScreen returnScreen, Font fallbackFont)
        {
            if (returnScreen == null)
            {
                throw new ArgumentNullException(nameof(returnScreen));
            }

            if (fallbackFont == null)
            {
                throw new ArgumentNullException(nameof(fallbackFont));
            }

            VoiceSettingsTheme? publicTheme = null;
            if (TryResolveFromMenuResources(out var resolvedPublicTheme, fallbackFont))
            {
                publicTheme = resolvedPublicTheme;
            }

            var sampleFallbackFont = publicTheme?.PrimaryFont ?? fallbackFont;
            if (TryResolveFromLoadedMenuObjects(returnScreen, sampleFallbackFont, out var sampledTheme))
            {
                return publicTheme == null ? sampledTheme : MergeThemes(publicTheme, sampledTheme);
            }

            return publicTheme ?? CreateFallbackTheme(fallbackFont);
        }

        private static bool TryResolveFromMenuResources(out VoiceSettingsTheme theme, Font fallbackFont)
        {
            MenuResources.ReloadResources();

            var primaryFont = MenuResources.NotoSerifCJKSCRegular
                ?? MenuResources.Perpetua
                ?? MenuResources.TrajanRegular
                ?? MenuResources.TrajanBold
                ?? fallbackFont;
            var secondaryFont = MenuResources.TrajanBold
                ?? MenuResources.TrajanRegular
                ?? primaryFont;
            var foundPublicResource = MenuResources.NotoSerifCJKSCRegular != null
                || MenuResources.Perpetua != null
                || MenuResources.TrajanRegular != null
                || MenuResources.TrajanBold != null;

            theme = new VoiceSettingsTheme(
                primaryFont,
                secondaryFont,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                VoiceSettingsWindowController.FullscreenDimColor,
                new Color(0.93f, 0.90f, 0.82f, 0.96f),
                new Color(0.89f, 0.85f, 0.76f, 0.96f),
                new Color(0.84f, 0.80f, 0.72f, 0.96f),
                new Color(0.79f, 0.75f, 0.67f, 0.96f),
                new Color(0.88f, 0.82f, 0.56f, 0.98f),
                new Color(0.72f, 0.69f, 0.61f, 0.98f),
                new Color(0.71f, 0.41f, 0.35f, 0.98f),
                new Color(0.96f, 0.94f, 0.89f, 1f),
                new Color(0.78f, 0.73f, 0.64f, 1f),
                new Color(0.67f, 0.63f, 0.55f, 0.94f),
                VoiceSettingsWindowController.SuccessTextColor,
                VoiceSettingsWindowController.ErrorTextColor,
                false,
                false,
                false,
                false,
                false);
            return foundPublicResource;
        }

        private static bool TryResolveFromLoadedMenuObjects(MenuScreen returnScreen, Font fallbackFont, out VoiceSettingsTheme theme)
        {
            var titleFont = FindBestMenuFont(returnScreen, "title", "label");
            var bodyFont = FindBestMenuFont(returnScreen, "description", "text", "content");
            var primaryFont = bodyFont ?? titleFont ?? fallbackFont;
            var secondaryFont = titleFont ?? bodyFont ?? primaryFont;

            var windowSprite = FindBestMenuSprite(returnScreen, "screen", "panel", "menu", "content");
            var sectionSprite = FindBestMenuSprite(returnScreen, "panel", "box", "content", "control");
            var rowSprite = FindBestMenuSprite(returnScreen, "button", "option", "row", "menu");
            var inputSprite = FindBestMenuSprite(returnScreen, "button", "option", "panel", "underline");
            var buttonSprite = FindBestMenuSprite(returnScreen, "button", "option", "menu", "flash");

            var foundSampledResource = titleFont != null
                || bodyFont != null
                || windowSprite != null
                || sectionSprite != null
                || rowSprite != null
                || inputSprite != null
                || buttonSprite != null;

            theme = new VoiceSettingsTheme(
                primaryFont,
                secondaryFont,
                windowSprite,
                sectionSprite ?? windowSprite,
                rowSprite ?? sectionSprite ?? windowSprite,
                inputSprite ?? rowSprite ?? sectionSprite,
                buttonSprite,
                buttonSprite,
                buttonSprite,
                VoiceSettingsWindowController.FullscreenDimColor,
                new Color(0.97f, 0.95f, 0.90f, 0.97f),
                new Color(0.93f, 0.90f, 0.83f, 0.97f),
                new Color(0.88f, 0.84f, 0.76f, 0.97f),
                new Color(0.82f, 0.78f, 0.70f, 0.97f),
                new Color(0.95f, 0.86f, 0.56f, 0.99f),
                new Color(0.74f, 0.70f, 0.61f, 0.99f),
                new Color(0.73f, 0.42f, 0.36f, 0.99f),
                new Color(0.96f, 0.94f, 0.89f, 1f),
                new Color(0.78f, 0.73f, 0.64f, 1f),
                new Color(0.67f, 0.63f, 0.55f, 0.94f),
                VoiceSettingsWindowController.SuccessTextColor,
                VoiceSettingsWindowController.ErrorTextColor,
                HasUsableBorder(windowSprite),
                HasUsableBorder(sectionSprite ?? windowSprite),
                HasUsableBorder(rowSprite ?? sectionSprite ?? windowSprite),
                HasUsableBorder(inputSprite ?? rowSprite ?? sectionSprite),
                HasUsableBorder(buttonSprite));
            return foundSampledResource;
        }

        private static VoiceSettingsTheme CreateFallbackTheme(Font fallbackFont)
        {
            return new VoiceSettingsTheme(
                fallbackFont,
                fallbackFont,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                VoiceSettingsWindowController.FullscreenDimColor,
                VoiceSettingsWindowController.WindowColor,
                VoiceSettingsWindowController.SectionColor,
                VoiceSettingsWindowController.RowColor,
                VoiceSettingsWindowController.InputColor,
                VoiceSettingsWindowController.PrimaryButtonColor,
                VoiceSettingsWindowController.SecondaryButtonColor,
                VoiceSettingsWindowController.DangerButtonColor,
                VoiceSettingsWindowController.TextColor,
                VoiceSettingsWindowController.MutedTextColor,
                VoiceSettingsWindowController.PlaceholderTextColor,
                VoiceSettingsWindowController.SuccessTextColor,
                VoiceSettingsWindowController.ErrorTextColor,
                false,
                false,
                false,
                false,
                false);
        }

        private static Sprite? FindBestMenuSprite(MenuScreen returnScreen, params string[] preferredNames)
        {
            Sprite? bestSprite = null;
            var bestScore = int.MinValue;
            var images = returnScreen.GetComponentsInChildren<Image>(true);
            for (var index = 0; index < images.Length; index++)
            {
                var image = images[index];
                if (image == null || image.sprite == null)
                {
                    continue;
                }

                var score = ScorePreferredMatch(image.gameObject.name, preferredNames)
                    + ScorePreferredMatch(image.sprite.name, preferredNames);
                if (image.type == Image.Type.Sliced)
                {
                    score += 4;
                }

                if (HasUsableBorder(image.sprite))
                {
                    score += 3;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSprite = image.sprite;
                }
            }

            return bestSprite;
        }

        private static Font? FindBestMenuFont(MenuScreen returnScreen, params string[] preferredNames)
        {
            Font? bestFont = null;
            var bestScore = int.MinValue;
            var texts = returnScreen.GetComponentsInChildren<Text>(true);
            for (var index = 0; index < texts.Length; index++)
            {
                var text = texts[index];
                if (text == null)
                {
                    continue;
                }

                var font = text.font;
                if (font == null)
                {
                    continue;
                }

                var score = ScorePreferredMatch(text.gameObject.name, preferredNames)
                    + ScorePreferredMatch(font.name, preferredNames);
                if (text.fontStyle == FontStyle.Bold)
                {
                    score += 2;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestFont = font;
                }
            }

            return bestFont;
        }

        private static int ScorePreferredMatch(string value, string[] preferredNames)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            var score = 1;
            for (var index = 0; index < preferredNames.Length; index++)
            {
                var preferredName = preferredNames[index];
                if (!string.IsNullOrWhiteSpace(preferredName)
                    && value.IndexOf(preferredName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score += 10 - index;
                }
            }

            return score;
        }

        private static bool HasUsableBorder(Sprite? sprite)
        {
            return sprite != null && sprite.border.sqrMagnitude > 0.01f;
        }

        private static VoiceSettingsTheme MergeThemes(VoiceSettingsTheme publicTheme, VoiceSettingsTheme sampledTheme)
        {
            return new VoiceSettingsTheme(
                publicTheme.PrimaryFont,
                publicTheme.SecondaryFont,
                sampledTheme.WindowSprite,
                sampledTheme.SectionSprite,
                sampledTheme.RowSprite,
                sampledTheme.InputSprite,
                sampledTheme.PrimaryButtonSprite,
                sampledTheme.SecondaryButtonSprite,
                sampledTheme.DangerButtonSprite,
                publicTheme.FullscreenDimColor,
                sampledTheme.WindowSprite != null ? sampledTheme.WindowTint : publicTheme.WindowTint,
                sampledTheme.SectionSprite != null ? sampledTheme.SectionTint : publicTheme.SectionTint,
                sampledTheme.RowSprite != null ? sampledTheme.RowTint : publicTheme.RowTint,
                sampledTheme.InputSprite != null ? sampledTheme.InputTint : publicTheme.InputTint,
                sampledTheme.PrimaryButtonSprite != null ? sampledTheme.PrimaryButtonTint : publicTheme.PrimaryButtonTint,
                sampledTheme.SecondaryButtonSprite != null ? sampledTheme.SecondaryButtonTint : publicTheme.SecondaryButtonTint,
                sampledTheme.DangerButtonSprite != null ? sampledTheme.DangerButtonTint : publicTheme.DangerButtonTint,
                publicTheme.TextColor,
                publicTheme.MutedTextColor,
                publicTheme.PlaceholderTextColor,
                publicTheme.SuccessTextColor,
                publicTheme.ErrorTextColor,
                sampledTheme.WindowSpriteIsSliced,
                sampledTheme.SectionSpriteIsSliced,
                sampledTheme.RowSpriteIsSliced,
                sampledTheme.InputSpriteIsSliced,
                sampledTheme.ButtonSpriteIsSliced);
        }
    }
}
