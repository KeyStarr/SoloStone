using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client
{
    internal static class B14K3EmeraldDreamPools
    {
        internal static OfflinePoolFilter Spell() => OfflinePoolFilter.Random(type: CardType.SPELL);
        [OfflinePool("EDR_520", 114293)]
        internal static PoolRequirement[] DeclarePools() => new[] {
            new PoolRequirement("spell", Spell(), new[] { "MAGE" }, PoolCostBinding.StackNumber, 3, destination: PoolDestination.Other)
        };
    }
}
