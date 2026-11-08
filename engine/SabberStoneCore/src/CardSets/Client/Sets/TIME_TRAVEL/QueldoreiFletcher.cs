using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class QueldoreiFletcher
    {
        internal const int DbfId = 119704;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Aura = new ConditionalHeroPowerCostAura(3, 0, "TIME_606e") };
        }
    }
}
