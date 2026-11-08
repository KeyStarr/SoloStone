using System;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    /// <summary>Current CORE-set Standard modules (HunterRogue slice).</summary>
    internal static class CoreHunterRogueCards
    {
        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            register(CoreWebspinner.DbfId, "CORE_FP1_011", CoreWebspinner.Configure);
            register(CoreBallOfSpiders.DbfId, "CORE_AT_062", CoreBallOfSpiders.Configure);
            register(CoreCrazedChemist.DbfId, "CORE_BOT_576", CoreCrazedChemist.Configure);
            register(CoreQuickShot.DbfId, "CORE_BRM_013", CoreQuickShot.Configure);
            register(CoreShakuTheCollector.DbfId, "CORE_CFM_781", CoreShakuTheCollector.Configure);
            register(CoreBackstab.DbfId, "CORE_CS2_072", CoreBackstab.Configure);
            register(CoreDeadlyPoison.DbfId, "CORE_CS2_074", CoreDeadlyPoison.Configure);
            register(CoreAssassinate.DbfId, "CORE_CS2_076", CoreAssassinate.Configure);
            register(CoreWagglePick.DbfId, "CORE_DAL_720", CoreWagglePick.Configure);
            register(CoreTracking.DbfId, "CORE_DS1_184", CoreTracking.Configure);
            register(CoreArcaneShot.DbfId, "CORE_DS1_185", CoreArcaneShot.Configure);
            register(CoreFanOfKnives.DbfId, "CORE_EX1_129", CoreFanOfKnives.Configure);
            register(CoreDefiasRingleader.DbfId, "CORE_EX1_131", CoreDefiasRingleader.Configure);
            register(CoreSI7Agent.DbfId, "CORE_EX1_134", CoreSI7Agent.Configure);
            register(1760, "EX1_145o", CorePreparation.ConfigureDiscount);
            register(CorePreparation.DbfId, "CORE_EX1_145", CorePreparation.Configure);
            register(CoreShiv.DbfId, "CORE_EX1_278", CoreShiv.Configure);
            register(CoreExplosiveTrap.DbfId, "CORE_EX1_610", CoreExplosiveTrap.Configure);
            register(CoreFreezingTrap.DbfId, "CORE_EX1_611", CoreFreezingTrap.Configure);
            register(CoreRatTrap.DbfId, "CORE_GIL_577", CoreRatTrap.Configure);
            register(CoreSwashburglar.DbfId, "CORE_KAR_069", CoreSwashburglar.Configure);
            register(CoreAnimalCompanion.DbfId, "CORE_NEW1_031", CoreAnimalCompanion.Configure);
            register(CoreCallOfTheWild.DbfId, "CORE_OG_211", CoreCallOfTheWild.Configure);
            register(CoreHeadhuntersHatchet.DbfId, "CORE_TRL_111", CoreHeadhuntersHatchet.Configure);
            register(CoreHalazziTheLynx.DbfId, "CORE_TRL_900", CoreHalazziTheLynx.Configure);
            register(CoreJeweledMacaw.DbfId, "CORE_UNG_912", CoreJeweledMacaw.Configure);
            register(Jackpot.DbfId, "CORE_TID_931", Jackpot.Configure);
            register(MountainBear.DbfId, "CORE_AV_337", MountainBear.Configure);
            register(WoundPrey.DbfId, "CORE_BAR_801", WoundPrey.Configure);
            register(AugmentedPorcupine.DbfId, "CORE_BT_201", AugmentedPorcupine.Configure);
            register(Spymistress.DbfId, "CORE_BT_701", Spymistress.Configure);
            register(SahketSapper.DbfId, "CORE_ULD_280", SahketSapper.Configure);
            register(Dragonbane.DbfId, "CORE_DRG_256", Dragonbane.Configure);
            register(FoxyFraud.DbfId, "CORE_DMF_511", FoxyFraud.Configure);
            register(PressurePlate.DbfId, "CORE_ULD_152", PressurePlate.Configure);
            register(71465, "AV_337t", card => card.Implemented = true);
            register(63459, "BAR_035t", card => card.Implemented = true);
            register(61152, "DMF_511e", FoxyFraud.ConfigureDiscount);
        }
    }
}
