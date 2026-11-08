using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MuradinsLastStand
    {
        internal const int DbfId = 122999;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.Power = new Power {
                Aura = new AdaptiveCostEffect(p => p.Game.HistoryFacts.Query.FriendlyAttacksReachedCombat(p.Controller.Id)),
                PowerTask = new DrawTask(2)
            };
        }
    }
}
