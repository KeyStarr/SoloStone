using System;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    /// <summary>Current CORE-set Standard modules (PriestPaladin slice).</summary>
    internal static class CorePriestPaladinCards
    {
        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            register(CaliaMenethil.DbfId, "CORE_CATA_002", CaliaMenethil.Configure);
            register(IvoryKnight.DbfId, "CORE_KAR_057", IvoryKnight.Configure);
            register(SilvermoonPortal.DbfId, "CORE_KAR_077", SilvermoonPortal.Configure);
            register(FlashHeal.DbfId, "CORE_AT_055", FlashHeal.Configure);
            register(GreaterHealingPotion.DbfId, "CORE_CFM_604", GreaterHealingPotion.Configure);
            register(GrimestreetOutfitter.DbfId, "CORE_CFM_753", GrimestreetOutfitter.Configure);
            register(HolyNova.DbfId, "CORE_CS1_112", HolyNova.Configure);
            register(HolySmite.DbfId, "CORE_CS1_130", HolySmite.Configure);
            register(PowerWordShield.DbfId, "CORE_CS2_004", PowerWordShield.Configure);
            register(Consecration.DbfId, "CORE_CS2_093", Consecration.Configure);
            register(HammerofWrath.DbfId, "CORE_CS2_094", HammerofWrath.Configure);
            register(ArgentProtector.DbfId, "CORE_EX1_362", ArgentProtector.Configure);
            register(TirionFordring.DbfId, "CORE_EX1_383", TirionFordring.Configure);
            register(Equality.DbfId, "CORE_EX1_619", Equality.Configure);
            register(Coghammer.DbfId, "CORE_GVG_059", Coghammer.Configure);
            register(MusterforBattle.DbfId, "CORE_GVG_061", MusterforBattle.Configure);
            register(RighteousProtector.DbfId, "CORE_ICC_038", RighteousProtector.Configure);
            register(ShadowAscendant.DbfId, "CORE_ICC_210", ShadowAscendant.Configure);
            register(ObsidianStatue.DbfId, "CORE_ICC_214", ObsidianStatue.Configure);
            register(FlashofLight.DbfId, "CORE_TRL_307", FlashofLight.Configure);
            register(SpikeridgedSteed.DbfId, "CORE_UNG_952", SpikeridgedSteed.Configure);
            register(BattleVicar.DbfId, "CORE_ONY_022", BattleVicar.Configure);
            register(LightshowerElemental.DbfId, "CORE_BAR_310", LightshowerElemental.Configure);
            register(VoidShard.DbfId, "CORE_SW_442", VoidShard.Configure);
            register(DevouringPlague.DbfId, "CORE_BAR_311", DevouringPlague.Configure);
            register(PsychicConjurer.DbfId, "CORE_EX1_193", PsychicConjurer.Configure);
            register(ShadowWordRuin.DbfId, "CORE_EX1_197", ShadowWordRuin.Configure);
            register(NatalieSeline.DbfId, "CORE_EX1_198", NatalieSeline.Configure);
            register(CrystalsmithCultist.DbfId, "CORE_RLK_814", CrystalsmithCultist.Configure);
            register(Initiation.DbfId, "CORE_SCH_512", Initiation.Configure);
            register(VeteranWarmedic.DbfId, "CORE_BAR_878", VeteranWarmedic.Configure);
            register(HandOfAdal.DbfId, "CORE_BT_292", HandOfAdal.Configure);
            register(HighlordFordragon.DbfId, "CORE_SW_047", HighlordFordragon.Configure);
            register(ImmortalizedInStone.DbfId, "CORE_TSC_076", ImmortalizedInStone.Configure);
            register(60075, "EX1_198e", NatalieSeline.ConfigureBuff);
            register(84205, "RLK_814e", CrystalsmithCultist.ConfigureBuff);
            register(57548, "BT_292e", HandOfAdal.ConfigureBuff);
            register(64547, "SW_047e", HighlordFordragon.ConfigureBuff);
            register(63321, "BAR_878t", card => card.Implemented = true);
            register(80116, "TSC_076t3", card => card.Implemented = true);
            register(80115, "TSC_076t2", card => card.Implemented = true);
            register(80113, "TSC_076t", card => card.Implemented = true);
        }
    }
}
