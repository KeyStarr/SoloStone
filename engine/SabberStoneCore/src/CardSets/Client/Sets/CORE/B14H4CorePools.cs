using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client
{
    internal static class B14H4CorePools
    {
        internal static OfflinePoolFilter Minion() => OfflinePoolFilter.Random(type:CardType.MINION);
        [OfflinePool("CORE_BOT_256", 129846)]
        internal static PoolRequirement[] MinionPools() => new[] {
            new PoolRequirement("hand-size", Minion(), new[] { "MAGE" }, PoolCostBinding.StackNumber, 3, destination: PoolDestination.Summon)
        };
    }
}
