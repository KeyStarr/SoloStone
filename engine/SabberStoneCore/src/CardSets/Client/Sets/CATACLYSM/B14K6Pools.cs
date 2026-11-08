using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client
{
    internal static class B14K6CataclysmPools
    {
        internal static OfflinePoolFilter Shatter() => new OfflinePoolFilter(classMode:PoolClassMode.OtherClass,keyword:(GameTag)4239);
        internal static OfflinePoolFilter OtherSpell() => new OfflinePoolFilter(classMode:PoolClassMode.OtherClass,type:CardType.SPELL);
        [OfflinePool("CATA_202",Sets.StolenPower.DbfId)]
        internal static PoolRequirement[] Stolen() => new[]{new PoolRequirement("other-class-shatter",Shatter(),new[]{"ROGUE"})};
        [OfflinePool("CATA_786",Sets.ChaosSupplicant.DbfId)]
        internal static PoolRequirement[] Supplicant() => new[]{new PoolRequirement("other-class-same-cost-spell",OtherSpell(),new[]{"ROGUE"},PoolCostBinding.StackNumber,1, destination: PoolDestination.Other)};
    }
}
