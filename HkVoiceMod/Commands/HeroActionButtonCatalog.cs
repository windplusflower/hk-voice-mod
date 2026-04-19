using System.Collections.Generic;

namespace HkVoiceMod.Commands
{
    internal static class HeroActionButtonCatalog
    {
        private static readonly global::GlobalEnums.HeroActionButton[] GameplayButtons =
        {
            global::GlobalEnums.HeroActionButton.LEFT,
            global::GlobalEnums.HeroActionButton.RIGHT,
            global::GlobalEnums.HeroActionButton.UP,
            global::GlobalEnums.HeroActionButton.DOWN,
            global::GlobalEnums.HeroActionButton.JUMP,
            global::GlobalEnums.HeroActionButton.ATTACK,
            global::GlobalEnums.HeroActionButton.DASH,
            global::GlobalEnums.HeroActionButton.SUPER_DASH,
            global::GlobalEnums.HeroActionButton.CAST,
            global::GlobalEnums.HeroActionButton.QUICK_CAST,
            global::GlobalEnums.HeroActionButton.DREAM_NAIL,
            global::GlobalEnums.HeroActionButton.QUICK_MAP,
            global::GlobalEnums.HeroActionButton.INVENTORY
        };

        public static IReadOnlyList<global::GlobalEnums.HeroActionButton> SupportedGameplayButtons => GameplayButtons;

        public static bool TryMapLegacyKey(HeroActionKey key, out global::GlobalEnums.HeroActionButton actionButton)
        {
            switch (key)
            {
                case HeroActionKey.Left:
                    actionButton = global::GlobalEnums.HeroActionButton.LEFT;
                    return true;
                case HeroActionKey.Right:
                    actionButton = global::GlobalEnums.HeroActionButton.RIGHT;
                    return true;
                case HeroActionKey.Up:
                    actionButton = global::GlobalEnums.HeroActionButton.UP;
                    return true;
                case HeroActionKey.Down:
                    actionButton = global::GlobalEnums.HeroActionButton.DOWN;
                    return true;
                case HeroActionKey.Attack:
                    actionButton = global::GlobalEnums.HeroActionButton.ATTACK;
                    return true;
                case HeroActionKey.Jump:
                    actionButton = global::GlobalEnums.HeroActionButton.JUMP;
                    return true;
                case HeroActionKey.Dash:
                    actionButton = global::GlobalEnums.HeroActionButton.DASH;
                    return true;
                case HeroActionKey.Cast:
                    actionButton = global::GlobalEnums.HeroActionButton.CAST;
                    return true;
                default:
                    actionButton = default(global::GlobalEnums.HeroActionButton);
                    return false;
            }
        }

        public static List<global::GlobalEnums.HeroActionButton> MapLegacyKeys(IReadOnlyList<HeroActionKey>? keys)
        {
            var mapped = new List<global::GlobalEnums.HeroActionButton>(keys?.Count ?? 0);
            if (keys == null)
            {
                return mapped;
            }

            for (var index = 0; index < keys.Count; index++)
            {
                if (TryMapLegacyKey(keys[index], out var actionButton))
                {
                    mapped.Add(actionButton);
                }
            }

            return mapped;
        }

        public static bool IsHorizontal(global::GlobalEnums.HeroActionButton actionButton)
        {
            return actionButton == global::GlobalEnums.HeroActionButton.LEFT || actionButton == global::GlobalEnums.HeroActionButton.RIGHT;
        }

        public static bool IsVertical(global::GlobalEnums.HeroActionButton actionButton)
        {
            return actionButton == global::GlobalEnums.HeroActionButton.UP || actionButton == global::GlobalEnums.HeroActionButton.DOWN;
        }
    }
}
