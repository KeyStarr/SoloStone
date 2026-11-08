using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client
{
    internal static class B14H4CataclysmPools
    {
        internal static OfflinePoolFilter Dragon() => OfflinePoolFilter.Random(type:CardType.MINION,race:Race.DRAGON);
        [OfflinePool("CATA_140", 122978)]
        internal static PoolRequirement[] DragonPools() => new[] {
            new PoolRequirement("dragon", Dragon(), new[] { "DRUID" })
        };
    }
}
