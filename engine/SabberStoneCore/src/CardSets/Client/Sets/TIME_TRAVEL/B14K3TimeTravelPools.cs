using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client
{
    internal static class B14K3TimeTravelPools
    {
        internal static OfflinePoolFilter Minion(int cost) => OfflinePoolFilter.Random(type: CardType.MINION, minCost: cost, maxCost: cost);
        [OfflinePool("TIME_859", 119691)]
        internal static PoolRequirement[] DeclarePools() => new[] {
            new PoolRequirement("ten-cost-minion", Minion(10), new[] { "MAGE" }, destination: PoolDestination.Summon),
            new PoolRequirement("one-cost-minion", Minion(1), new[] { "MAGE" }, destination: PoolDestination.Summon)
        };
    }
}
