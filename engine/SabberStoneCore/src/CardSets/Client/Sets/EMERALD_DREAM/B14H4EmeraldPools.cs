using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client
{
    internal static class B14H4EmeraldPools
    {
        internal static OfflinePoolFilter Nature() => OfflinePoolFilter.Discover(type:CardType.SPELL,spellSchool:4);
        [OfflinePool("EDR_270", 115080)]
        internal static PoolRequirement[] NaturePools() => new[] {
            new PoolRequirement("nature-spell", Nature(), new[] { "DRUID" })
        };
    }
}
