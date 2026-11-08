using SabberStoneCore.Enums;
using SabberStoneCore.Model;
namespace SabberStoneCore.CardSets.Client
{
 internal static class CoreHistoryExtPools
 {
 internal static OfflinePoolFilter Unholy()=>OfflinePoolFilter.Random(keyword:GameTag.COST_UNHOLY);
 [OfflinePool("CORE_RLK_116",111328)]
 internal static PoolRequirement[] Declare()=>new[]{new PoolRequirement("unholy-rune",Unholy(),new[]{"DEATHKNIGHT"})};
 }
}
