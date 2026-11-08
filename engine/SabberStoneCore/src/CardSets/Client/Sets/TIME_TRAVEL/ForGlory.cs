using SabberStoneCore.Auras;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ForGlory
    {
        internal const int DbfId = 120828;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                Aura = new AdaptiveCostEffect(p => p.Controller.Opponent.BoardZone.Count),
                PowerTask = new DrawTask(2)
            };
        }
    }
}
