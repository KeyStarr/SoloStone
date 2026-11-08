using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client
{
    internal static class B14K3BEPools
    {
        internal static OfflinePoolFilter Spell() => OfflinePoolFilter.Discover(type: CardType.SPELL);
        [OfflinePool("BE_150", 130441)]
        internal static PoolRequirement[] DeclarePools() => new[] {
            new PoolRequirement("spell", Spell(), new[] { "MAGE" }, destination: PoolDestination.Deck)
        };
    }
}
