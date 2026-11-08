using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TheUnseenAtlas
    {
        internal const int DbfId = 126477;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                Aura = new AdaptiveCostEffect(p => CostRules.HandCountForCost(p)),
                PowerTask = new CustomTask((g, c, s, t, stack) => {
                    for (int i = 0; i < 3; i++) Generic.Draw(c);
                })
            };
        }
    }
}
