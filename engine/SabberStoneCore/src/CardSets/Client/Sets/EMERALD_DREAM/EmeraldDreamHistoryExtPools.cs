using SabberStoneCore.Enums;
using SabberStoneCore.Model;
namespace SabberStoneCore.CardSets.Client
{
 internal static class EmeraldDreamHistoryExtPools
 {
 internal static OfflinePoolFilter TwoCost()=>OfflinePoolFilter.Random(type:CardType.MINION,minCost:2,maxCost:2);
 [OfflinePool("EDR_781",115176)]
 internal static PoolRequirement[] Declare()=>new[]{new PoolRequirement("two-cost-minion",TwoCost(),new[]{"ROGUE"}, destination: PoolDestination.Summon)};
 }
}
