using System;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    /// <summary>Current CORE-set Standard modules (NeutralA slice).</summary>
    internal static class CoreNeutralACards
    {
        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            register(Chillmaw.DbfId, "CORE_AT_123", Chillmaw.Configure);
            register(FinjaTheFlyingStar.DbfId, "CORE_CFM_344", FinjaTheFlyingStar.Configure);
            register(DirtyRat.DbfId, "CORE_CFM_790", DirtyRat.Configure);
            register(RaidLeader.DbfId, "CORE_CS2_122", RaidLeader.Configure);
            register(AbusiveSergeant.DbfId, "CORE_CS2_188", AbusiveSergeant.Configure);
            register(TheBlackKnight.DbfId, "CORE_EX1_002", TheBlackKnight.Configure);
            register(WorgenInfiltrator.DbfId, "CORE_EX1_010", WorgenInfiltrator.Configure);
            register(BloodmageThalnos.DbfId, "CORE_EX1_012", BloodmageThalnos.Configure);
            register(KingMukla.DbfId, "CORE_EX1_014", KingMukla.Configure);
            register(StranglethornTiger.DbfId, "CORE_EX1_028", StranglethornTiger.Configure);
            register(TwilightDrake.DbfId, "CORE_EX1_043", TwilightDrake.Configure);
            register(SunfuryProtector.DbfId, "CORE_EX1_058", SunfuryProtector.Configure);
            register(CrazedAlchemist.DbfId, "CORE_EX1_059", CrazedAlchemist.Configure);
            register(MadBomber.DbfId, "CORE_EX1_082", MadBomber.Configure);
            register(LorewalkerCho.DbfId, "CORE_EX1_100", LorewalkerCho.Configure);
            register(ColdlightSeer.DbfId, "CORE_EX1_103", ColdlightSeer.Configure);
            register(DireWolfAlpha.DbfId, "CORE_EX1_162", DireWolfAlpha.Configure);
            register(Brightwing.DbfId, "CORE_EX1_189", Brightwing.Configure);
            register(MurlocTidehunter.DbfId, "CORE_EX1_506", MurlocTidehunter.Configure);
            register(MurlocWarleader.DbfId, "CORE_EX1_507", MurlocWarleader.Configure);
            register(MurlocTidecaller.DbfId, "CORE_EX1_509", MurlocTidecaller.Configure);
            register(AnnoyOTron.DbfId, "CORE_GVG_085", AnnoyOTron.Configure);
            register(CairneBloodhoof.DbfId, "CORE_EX1_110", CairneBloodhoof.Configure);
            register(LootHoarder.DbfId, "CORE_EX1_096", LootHoarder.Configure);
            register(VoodooDoctor.DbfId, "CORE_EX1_011", VoodooDoctor.Configure);
            register(AcolyteOfPain.DbfId, "CORE_EX1_007", AcolyteOfPain.Configure);
            register(BigGameHunter.DbfId, "CORE_EX1_005", BigGameHunter.Configure);
            register(StormwindChampion.DbfId, "CORE_CS2_222", StormwindChampion.Configure);
            register(ElvenArcher.DbfId, "CORE_CS2_189", ElvenArcher.Configure);
            register(SenjinShieldmasta.DbfId, "CORE_CS2_179", SenjinShieldmasta.Configure);
            register(PrizeVendor.DbfId, "CORE_DMF_067", PrizeVendor.Configure);
            register(EvasiveWyrm.DbfId, "CORE_DRG_079", EvasiveWyrm.Configure);
            register(BlowtorchSaboteur.DbfId, "CORE_DRG_403", BlowtorchSaboteur.Configure);
            register(56227, "DRG_403e", BlowtorchSaboteur.ConfigureTax);
            register(FaerieDragon.DbfId, "CORE_NEW1_023", FaerieDragon.Configure);
            register(CultNeophyte.DbfId, "CORE_SCH_713", CultNeophyte.Configure);
            register(60044, "SCH_713e", CultNeophyte.ConfigureTax);
            register(MoargForgefiend.DbfId, "CORE_SW_068", MoargForgefiend.Configure);
        }
    }
}
