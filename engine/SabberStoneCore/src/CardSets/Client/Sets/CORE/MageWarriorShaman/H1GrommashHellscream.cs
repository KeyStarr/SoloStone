using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class H1GrommashHellscream
    {
        [StandardCard("CORE_EX1_414", 69643)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Aura = new EnrageEffect(AuraType.SELF, "EX1_414e") };
        }
    }
}
