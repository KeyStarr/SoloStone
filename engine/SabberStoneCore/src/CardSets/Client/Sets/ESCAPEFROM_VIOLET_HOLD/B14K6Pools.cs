using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client
{
    internal static class B14K6JailPools
    {
        internal static OfflinePoolFilter EightCostMinion() => OfflinePoolFilter.Random(type:CardType.MINION,minCost:8,maxCost:8);
        [OfflinePool("JAIL_474",Sets.JadeGuardians.DbfId)]
        internal static PoolRequirement[] Guardians() => new[]{new PoolRequirement("eight-cost-minion",EightCostMinion(),new[]{"ROGUE"})};
    }
}
