using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RemnantofRage
    {
        internal const int DbfId = 121058;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Game.ProcessDeaths increments this tag for both sides; BeginTurn resets it.
            card.Power = new Power {
                Aura = new AdaptiveCostEffect(p => p.Game.NumMinionsKilledThisTurn),
                PowerTask = ComplexTask.Create(new DrawTask(), new DrawTask())
            };
        }
    }
}
