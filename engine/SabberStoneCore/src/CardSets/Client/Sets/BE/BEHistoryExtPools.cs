using SabberStoneCore.Enums;
using SabberStoneCore.Model;
namespace SabberStoneCore.CardSets.Client
{
 internal static class BEHistoryExtPools
 {
 internal static OfflinePoolFilter Spell()=>OfflinePoolFilter.Random(type:CardType.SPELL);
 [OfflinePool("BE_997",132135)]
 internal static PoolRequirement[] Declare()=>new[]{new PoolRequirement("shared-spell",Spell(),new[]{"NEUTRAL"})};
 }
}
