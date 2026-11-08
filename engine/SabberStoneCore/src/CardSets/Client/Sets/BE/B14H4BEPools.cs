using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client
{
    internal static class B14H4BEPools
    {
        internal static OfflinePoolFilter Five() => OfflinePoolFilter.Random(type:CardType.MINION,minCost:5,maxCost:5);
        [OfflinePool("BE_780", 131126)]
        internal static PoolRequirement[] FivePools() => new[] {
            new PoolRequirement("five-cost", Five(), new[] { "DEATHKNIGHT" }, destination: PoolDestination.Summon)
        };
        internal static OfflinePoolFilter Minion() => OfflinePoolFilter.Random(type:CardType.MINION);
        [OfflinePool("BE_781", 131127)]
        internal static PoolRequirement[] MinionPools() => new[] {
            new PoolRequirement("unholy-cost", Minion(), new[] { "DEATHKNIGHT" }, PoolCostBinding.StackNumber, 3, destination: PoolDestination.Summon)
        };
    }
}
