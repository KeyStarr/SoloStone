using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.Actions
{
    /// <summary>Printed deck membership, including modern class masks and historical faction groups.</summary>
    public static class CardClassRules
    {
        public static bool IsAllowedInDeck(Card card, CardClass heroClass)
        {
            if (card == null) return false;
            int mask = card[GameTag.MULTIPLE_CLASSES];
            int value = (int)heroClass;
            if (mask != 0) return value > 0 && value <= 32 && (mask & (1 << (value - 1))) != 0;
            switch ((MultiClassGroup)card.MultiClassGroup)
            {
                case MultiClassGroup.GRIMY_GOONS:
                    return heroClass == CardClass.HUNTER || heroClass == CardClass.PALADIN || heroClass == CardClass.WARRIOR;
                case MultiClassGroup.JADE_LOTUS:
                    return heroClass == CardClass.DRUID || heroClass == CardClass.ROGUE || heroClass == CardClass.SHAMAN;
                case MultiClassGroup.KABAL:
                    return heroClass == CardClass.MAGE || heroClass == CardClass.PRIEST || heroClass == CardClass.WARLOCK;
                default: return card.Class == heroClass || card.Class == CardClass.NEUTRAL;
            }
        }
    }
}
