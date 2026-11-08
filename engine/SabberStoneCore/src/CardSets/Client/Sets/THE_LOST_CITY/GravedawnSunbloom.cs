using SabberStoneCore.Auras;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GravedawnSunbloom
    {
        internal const int DbfId = 117317;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                Aura = new AdaptiveCostEffect(p => KindredRules.HandDiscount(p, 2)),
                PowerTask = new DrawTask(2)
            };
        }
    }
}
