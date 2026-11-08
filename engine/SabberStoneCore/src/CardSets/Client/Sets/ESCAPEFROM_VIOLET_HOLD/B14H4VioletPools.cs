using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client
{
    internal static class B14H4VioletPools
    {
        internal static OfflinePoolFilter Minion() => OfflinePoolFilter.Random(type:CardType.MINION);
        [OfflinePool("JAIL_200", 127815)]
        internal static PoolRequirement[] MinionPools() => new[] {
            new PoolRequirement("hero-attack-cost", Minion(), new[] { "DRUID" }, PoolCostBinding.StackNumber, 2, destination: PoolDestination.Summon)
        };
    }
}
