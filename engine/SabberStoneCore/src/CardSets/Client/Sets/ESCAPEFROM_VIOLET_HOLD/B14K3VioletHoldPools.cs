using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client
{
    internal static class B14K3VioletHoldPools
    {
        internal static OfflinePoolFilter Minion() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 8, maxCost: 8);
        [OfflinePool("JAIL_735", 126841)]
        internal static PoolRequirement[] DeclarePools() => new[] {
            new PoolRequirement("minion", Minion(), new[] { "MAGE" }, destination: PoolDestination.Summon)
        };
    }
}
