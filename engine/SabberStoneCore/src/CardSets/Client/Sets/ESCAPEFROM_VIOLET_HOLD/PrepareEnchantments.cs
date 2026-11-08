using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PrepareEnchantments
    {
        internal static void ConfigureDiscount(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.ReduceCost(0)) { UseScriptTag = true } };
        }
        internal static void ConfigureLock(Card card)
        {
            card.Implemented = true;
            card.Tags[GameTag.TAG_ONE_TURN_EFFECT] = 1;
            card.Power = new Power();
        }
        internal static void ConfigureGrant(Card card)
        {
            card.Implemented = true;
            card.Power = new Power();
        }
    }
}
