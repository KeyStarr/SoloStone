using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Arachnathid
    {
        [StandardCard("JAIL_459", 127996)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Aura = new Aura(AuraType.BOARD, "JAIL_459e") };
        }
        [StandardCard("JAIL_459e", 127997)]
        internal static void ConfigureVenom(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(new Effect(GameTag.POISONOUS, EffectOperator.ADD, 1)) };
        }
    }
}
