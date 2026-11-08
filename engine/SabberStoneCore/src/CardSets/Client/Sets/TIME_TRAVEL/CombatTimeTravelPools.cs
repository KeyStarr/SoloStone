using SabberStoneCore.Enums;
using SabberStoneCore.Model;
namespace SabberStoneCore.CardSets.Client
{
    internal static class CombatTimeTravelPools
    {
        internal static OfflinePoolFilter EightCostMinion()=>OfflinePoolFilter.Random(type:CardType.MINION,minCost:8,maxCost:8);
        [OfflinePool("TIME_890t2",120068)]
        internal static PoolRequirement[] DeclarePools()=>new[]{
            new PoolRequirement("eight-cost-minion",EightCostMinion(),new[]{"PRIEST"}, destination: PoolDestination.Summon)
        };
    }
}
