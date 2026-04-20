using System;
using UnityEngine;
using UnityEngine.UI;

namespace HkVoiceMod.UI
{
    internal sealed class VoiceSettingsTheme
    {
        public VoiceSettingsTheme(
            Font primaryFont,
            Font secondaryFont,
            Sprite? windowSprite,
            Sprite? sectionSprite,
            Sprite? rowSprite,
            Sprite? inputSprite,
            Sprite? primaryButtonSprite,
            Sprite? secondaryButtonSprite,
            Sprite? dangerButtonSprite,
            Color fullscreenDimColor,
            Color windowTint,
            Color sectionTint,
            Color rowTint,
            Color inputTint,
            Color primaryButtonTint,
            Color secondaryButtonTint,
            Color dangerButtonTint,
            Color textColor,
            Color mutedTextColor,
            Color placeholderTextColor,
            Color successTextColor,
            Color errorTextColor,
            bool windowSpriteIsSliced,
            bool sectionSpriteIsSliced,
            bool rowSpriteIsSliced,
            bool inputSpriteIsSliced,
            bool buttonSpriteIsSliced)
        {
            PrimaryFont = primaryFont ?? throw new ArgumentNullException(nameof(primaryFont));
            SecondaryFont = secondaryFont ?? throw new ArgumentNullException(nameof(secondaryFont));
            WindowSprite = windowSprite;
            SectionSprite = sectionSprite;
            RowSprite = rowSprite;
            InputSprite = inputSprite;
            PrimaryButtonSprite = primaryButtonSprite;
            SecondaryButtonSprite = secondaryButtonSprite;
            DangerButtonSprite = dangerButtonSprite;
            FullscreenDimColor = fullscreenDimColor;
            WindowTint = windowTint;
            SectionTint = sectionTint;
            RowTint = rowTint;
            InputTint = inputTint;
            PrimaryButtonTint = primaryButtonTint;
            SecondaryButtonTint = secondaryButtonTint;
            DangerButtonTint = dangerButtonTint;
            TextColor = textColor;
            MutedTextColor = mutedTextColor;
            PlaceholderTextColor = placeholderTextColor;
            SuccessTextColor = successTextColor;
            ErrorTextColor = errorTextColor;
            WindowSpriteIsSliced = windowSpriteIsSliced;
            SectionSpriteIsSliced = sectionSpriteIsSliced;
            RowSpriteIsSliced = rowSpriteIsSliced;
            InputSpriteIsSliced = inputSpriteIsSliced;
            ButtonSpriteIsSliced = buttonSpriteIsSliced;
        }

        public Font PrimaryFont { get; }

        public Font SecondaryFont { get; }

        public Sprite? WindowSprite { get; }

        public Sprite? SectionSprite { get; }

        public Sprite? RowSprite { get; }

        public Sprite? InputSprite { get; }

        public Sprite? PrimaryButtonSprite { get; }

        public Sprite? SecondaryButtonSprite { get; }

        public Sprite? DangerButtonSprite { get; }

        public Color FullscreenDimColor { get; }

        public Color WindowTint { get; }

        public Color SectionTint { get; }

        public Color RowTint { get; }

        public Color InputTint { get; }

        public Color PrimaryButtonTint { get; }

        public Color SecondaryButtonTint { get; }

        public Color DangerButtonTint { get; }

        public Color TextColor { get; }

        public Color MutedTextColor { get; }

        public Color PlaceholderTextColor { get; }

        public Color SuccessTextColor { get; }

        public Color ErrorTextColor { get; }

        public bool WindowSpriteIsSliced { get; }

        public bool SectionSpriteIsSliced { get; }

        public bool RowSpriteIsSliced { get; }

        public bool InputSpriteIsSliced { get; }

        public bool ButtonSpriteIsSliced { get; }

        public ColorBlock CreatePrimaryButtonColors()
        {
            return CreateButtonColors(PrimaryButtonTint);
        }

        public ColorBlock CreateSecondaryButtonColors()
        {
            return CreateButtonColors(SecondaryButtonTint);
        }

        public ColorBlock CreateDangerButtonColors()
        {
            return CreateButtonColors(DangerButtonTint);
        }

        public ColorBlock CreateInputColors()
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = InputTint;
            colors.highlightedColor = Color.Lerp(InputTint, Color.white, 0.05f);
            colors.pressedColor = Color.Lerp(InputTint, Color.black, 0.08f);
            colors.selectedColor = Color.Lerp(InputTint, Color.white, 0.03f);
            colors.disabledColor = new Color(InputTint.r, InputTint.g, InputTint.b, 0.35f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            return colors;
        }

        public Sprite? GetButtonSprite(VoiceThemeButtonKind kind)
        {
            switch (kind)
            {
                case VoiceThemeButtonKind.Primary:
                    return PrimaryButtonSprite;
                case VoiceThemeButtonKind.Danger:
                    return DangerButtonSprite;
                default:
                    return SecondaryButtonSprite;
            }
        }

        public Color GetButtonTint(VoiceThemeButtonKind kind)
        {
            switch (kind)
            {
                case VoiceThemeButtonKind.Primary:
                    return PrimaryButtonTint;
                case VoiceThemeButtonKind.Danger:
                    return DangerButtonTint;
                default:
                    return SecondaryButtonTint;
            }
        }

        private static ColorBlock CreateButtonColors(Color baseColor)
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = baseColor;
            colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.08f);
            colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.12f);
            colors.selectedColor = Color.Lerp(baseColor, Color.white, 0.05f);
            colors.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.42f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            return colors;
        }
    }
}
