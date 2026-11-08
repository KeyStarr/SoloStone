using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
namespace SabberStoneCore.CardSets.Client
{
    internal static class CombatEmeraldDreamPools
    {
        internal static OfflinePoolFilter FireBudget()=>OfflinePoolFilter.Random(type:CardType.SPELL,spellSchool:2,maxCost:15);
        [OfflinePool("FIR_959",Sets.FyrakkTheBlazing.DbfId)]
        internal static PoolRequirement[] DeclarePools()=>new[]{
            new PoolRequirement("fire-budget",FireBudget(),new[]{"NEUTRAL"},PoolCostBinding.StackNumberMaxCost,ordinaryCost:15, destination: PoolDestination.Other)
        };
    }
}
