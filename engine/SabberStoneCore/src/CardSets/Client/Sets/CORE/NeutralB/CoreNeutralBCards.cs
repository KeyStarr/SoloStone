using System;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    /// <summary>Current CORE-set Standard modules (NeutralB slice).</summary>
    internal static class CoreNeutralBCards
    {
        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            register(MenagerieMug.DbfId, "CORE_WON_141", MenagerieMug.Configure);
            register(102822, "WON_141e", MenagerieMug.ConfigureBuff);
            register(RaptorHerald.DbfId, "CORE_EDR_004_2026", RaptorHerald.Configure);
            register(RaptorHerald.OriginalDbfId, "CORE_EDR_004", RaptorHerald.Configure);
            register(HenchClanThug.DbfId, "CORE_GIL_534", HenchClanThug.Configure);
            register(SwampLeech.DbfId, "CORE_GIL_558", SwampLeech.Configure);
            register(Lifedrinker.DbfId, "CORE_GIL_622", Lifedrinker.Configure);
            register(WitchwoodGrizzly.DbfId, "CORE_GIL_623", WitchwoodGrizzly.Configure);
            register(MicroMachine.DbfId, "CORE_GVG_103", MicroMachine.Configure);
            register(TheCurator.DbfId, "CORE_KAR_061", TheCurator.Configure);
            register(NetherspiteHistorian.DbfId, "CORE_KAR_062", NetherspiteHistorian.Configure);
            register(GorillabotA3.DbfId, "CORE_LOE_039", GorillabotA3.Configure);
            register(SleepyDragon.DbfId, "CORE_LOOT_137", SleepyDragon.Configure);
            register(BloodsailRaider.DbfId, "CORE_NEW1_018", BloodsailRaider.Configure);
            register(WildPyromancer.DbfId, "CORE_NEW1_020", WildPyromancer.Configure);
            register(Doomsayer.DbfId, "CORE_NEW1_021", Doomsayer.Configure);
            register(DreadCorsair.DbfId, "CORE_NEW1_022", DreadCorsair.Configure);
            register(SouthseaCaptain.DbfId, "CORE_NEW1_027", SouthseaCaptain.Configure);
            register(1852, "NEW1_027e", SouthseaCaptain.ConfigureBuff);
            register(Murmy.DbfId, "CORE_ULD_723", Murmy.Configure);
            register(FirePlumePhoenix.DbfId, "CORE_UNG_084", FirePlumePhoenix.Configure);
            register(GlacialShard.DbfId, "CORE_UNG_205", GlacialShard.Configure);
            register(FireFly.DbfId, "CORE_UNG_809", FireFly.Configure);
            register(PrimordialDrake.DbfId, "CORE_UNG_848", PrimordialDrake.Configure);
            register(TarCreeper.DbfId, "CORE_UNG_928", TarCreeper.Configure);
            register(BoulderfistOgre.DbfId, "Core_CS2_200", BoulderfistOgre.Configure);
            register(RoyalLibrarian.DbfId, "CORE_SW_066", RoyalLibrarian.Configure);
            register(PlatedBeetle.DbfId, "CORE_LOOT_413", PlatedBeetle.Configure);
            register(SneedsOldShredder.DbfId, "CORE_GVG_114", SneedsOldShredder.Configure);
            register(54152, "ULD_178a4", Siamat.ConfigureOption);
            register(54151, "ULD_178a3", Siamat.ConfigureOption);
            register(54150, "ULD_178a2", Siamat.ConfigureOption);
            register(54149, "ULD_178a", Siamat.ConfigureOption);
            register(Siamat.DbfId, "CORE_ULD_178", Siamat.Configure);
            register(TaelanFordring.DbfId, "CS3_024", TaelanFordring.Configure);
            register(MazeGuide.DbfId, "CORE_REV_308", MazeGuide.Configure);
            register(RustrotViper.DbfId, "CORE_SW_072", RustrotViper.Configure);
            register(BeamingSidekick.DbfId, "CORE_ULD_191", BeamingSidekick.Configure);
            register(InjuredTolvir.DbfId, "CORE_ULD_271", InjuredTolvir.Configure);
            register(SmallTimeBuccaneer.DbfId, "CORE_WON_351", SmallTimeBuccaneer.Configure);
            register(RunawayBlackwing.DbfId, "CORE_YOP_034", RunawayBlackwing.Configure);
            register(OverlordRunthak.DbfId, "CS3_025", OverlordRunthak.Configure);
            register(RedgillRazorjaw.DbfId, "CS3_038", RedgillRazorjaw.Configure);
            register(54268, "ULD_191e", BeamingSidekick.ConfigureBuff);
            register(66960, "CS3_025e", OverlordRunthak.ConfigureBuff);
        }
    }
}
