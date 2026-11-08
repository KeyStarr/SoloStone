using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client
{
    internal static class BEPastPools
    {
        [OfflinePool("BE_588",132009)]
        internal static PoolRequirement[] DeclarePools() => new[] {
                new PoolRequirement("tech-standard",OfflinePoolFilter.Random(type:CardType.MINION),new[]{"HUNTER"}),
                new PoolRequirement("tech-past",OfflinePoolFilter.Random(type:CardType.MINION,scope:PoolScope.Past),new[]{"HUNTER"})
        };
    }
}
