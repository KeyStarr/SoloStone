using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client
{
    internal static class B14H4TimePools
    {
        internal static OfflinePoolFilter Four() => OfflinePoolFilter.Random(type:CardType.MINION,minCost:4,maxCost:4);
        [OfflinePool("END_005", 121060)]
        internal static PoolRequirement[] FourPools() => new[] {
            new PoolRequirement("four-cost", Four(), new[] { "DEATHKNIGHT", "DEMONHUNTER" }, destination: PoolDestination.Summon)
        };
    }
}
