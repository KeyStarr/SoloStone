using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client
{
    internal static class B15PoolFiltersPools
    {
        [OfflinePool("BE_703", Sets.Drestagath.DbfId)]
        internal static PoolRequirement[] Drestagath() => Sets.Drestagath.DeclarePools();
        [OfflinePool("BE_298", Sets.ScionOfInsight.DbfId)]
        internal static PoolRequirement[] ScionOfInsight() => Sets.ScionOfInsight.DeclarePools();
        [OfflinePool("CATA_567", Sets.Ascendance.DbfId)]
        internal static PoolRequirement[] Ascendance() => Sets.Ascendance.DeclarePools();
        [OfflinePool("MEND_100t", Sets.CultivatingSprite.BulbDbfId)]
        internal static PoolRequirement[] CultivatingSprite() => Sets.CultivatingSprite.DeclarePools();
        [OfflinePool("CATA_621", Sets.GelbinsTriumph.DbfId)]
        internal static PoolRequirement[] GelbinsTriumph() => Sets.GelbinsTriumph.DeclarePools();
        [OfflinePool("CORE_WW_374", Sets.CorpseFarm.DbfId)]
        internal static PoolRequirement[] CorpseFarm() => Sets.CorpseFarm.DeclarePools();
        [OfflinePool("EDR_888", Sets.MalorneTheWaywatcher.DbfId)]
        internal static PoolRequirement[] MalorneTheWaywatcher() => Sets.MalorneTheWaywatcher.DeclarePools();
        [OfflinePool("FIR_920", Sets.SmokeBomb.DbfId)]
        internal static PoolRequirement[] SmokeBomb() => Sets.SmokeBomb.DeclarePools();
        [OfflinePool("JAIL_732", Sets.VoidSoul.DbfId)]
        internal static PoolRequirement[] VoidSoul() => Sets.VoidSoul.DeclarePools();
        [OfflinePool("DINO_427", Sets.CostumeMerchant.DbfId)]
        internal static PoolRequirement[] CostumeMerchant() => Sets.CostumeMerchant.DeclarePools();
        [OfflinePool("TLC_824", Sets.OddMap.DbfId)]
        internal static PoolRequirement[] OddMap() => Sets.OddMap.DeclarePools();
    }
}
