using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ScaledLancer
    {
        [StandardCard("CATA_898", 123655)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Aura = new Aura(AuraType.OP_BOARD, "CATA_898e") };
        }
        [StandardCard("CATA_898e", 123656)]
        internal static void ConfigureAura(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(new Effect(GameTag.TAUNT, EffectOperator.ADD, 1)) };
        }
    }
}
