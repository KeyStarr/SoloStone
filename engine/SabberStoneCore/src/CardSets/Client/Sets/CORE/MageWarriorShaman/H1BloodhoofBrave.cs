using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class H1BloodhoofBrave
    {
        [StandardCard("CORE_OG_218", 76303)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Aura = new EnrageEffect(AuraType.SELF, "OG_218e") };
        }
    }
}
