using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GronnGiant
    {
        internal const int DbfId = 123440;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.ConsumesHistory = true;
            card.Power = new Power { Aura = new AdaptiveCostEffect(p => CostRules.LastPlayedCost(p.Controller)) };
        }
    }
}
