using System;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    /// <summary>Current CORE-set Standard modules (MageWarriorShaman slice).</summary>
    internal static class CoreMageWarriorShamanCards
    {
        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            register(WarmaulChallenger.DbfId, "CORE_BT_120", WarmaulChallenger.Configure);
            register(KragwatheFrog.DbfId, "CORE_TRL_345", KragwatheFrog.Configure);
            register(VoltaicBurst.DbfId, "CORE_BOT_451", VoltaicBurst.Configure);
            register(FireElemental.DbfId, "CORE_CS2_042", FireElemental.Configure);
            register(FarSight.DbfId, "CORE_CS2_053", FarSight.Configure);
            register(LightningBolt.DbfId, "CORE_EX1_238", LightningBolt.Configure);
            register(Hex.DbfId, "CORE_EX1_246", Hex.Configure);
            register(LightningStorm.DbfId, "CORE_EX1_259", LightningStorm.Configure);
            register(ArchmageAntonidas.DbfId, "CORE_EX1_559", ArchmageAntonidas.Configure);
            register(WitchsApprentice.DbfId, "CORE_GIL_531", WitchsApprentice.Configure);
            register(BlazingInvocation.DbfId, "CORE_GIL_836", BlazingInvocation.Configure);
            register(BladedGauntlet.DbfId, "CORE_LOOT_044", BladedGauntlet.Configure);
            register(ExplosiveRunes.DbfId, "CORE_LOOT_101", ExplosiveRunes.Configure);
            register(RavagingGhoul.DbfId, "CORE_OG_149", RavagingGhoul.Configure);
            register(HammerOfTwilight.DbfId, "CORE_OG_031", HammerOfTwilight.Configure);
            register(ShieldBlock.DbfId, "CORE_EX1_606", ShieldBlock.Configure);
            register(FrothingBerserker.DbfId, "CORE_EX1_604", FrothingBerserker.Configure);
            register(Slam.DbfId, "CORE_EX1_391", Slam.Configure);
            register(IceBarrier.DbfId, "CORE_EX1_289", IceBarrier.Configure);
            register(Counterspell.DbfId, "CORE_EX1_287", Counterspell.Configure);
            register(Execute.DbfId, "CORE_CS2_108", Execute.Configure);
            register(Flamestrike.DbfId, "CORE_CS2_032", Flamestrike.Configure);
            register(Fireball.DbfId, "CORE_CS2_029", Fireball.Configure);
            register(Blizzard.DbfId, "CORE_CS2_028", Blizzard.Configure);
            register(Frostbolt.DbfId, "CORE_CS2_024", Frostbolt.Configure);
            register(Bash.DbfId, "CORE_AT_064", Bash.Configure);
            register(106455, "CFM_940e", IKnowAGuy.ConfigureBuff);
            register(IKnowAGuy.DbfId, "CORE_WON_350", IKnowAGuy.Configure);
            register(BabblingBookcase.DbfId, "CORE_EDR_001", BabblingBookcase.Configure);
            register(SkyRaider.DbfId, "CORE_DRG_024", SkyRaider.Configure);
            register(RunedOrb.DbfId, "CORE_BAR_541", RunedOrb.Configure);
            register(Glaciate.DbfId, "CORE_AV_107", Glaciate.Configure);
            register(IronforgePortal.DbfId, "CORE_WON_337", IronforgePortal.Configure);
            register(OasisAlly.DbfId, "CORE_BAR_812", OasisAlly.Configure);
            register(DeepFreeze.DbfId, "CORE_BT_072", DeepFreeze.Configure);
            register(VioletSpellwing.DbfId, "CORE_DRG_107", VioletSpellwing.Configure);
            register(FirstFlame.DbfId, "CORE_SW_108", FirstFlame.Configure);
            register(Hookfist3000.DbfId, "CORE_NX2_028", Hookfist3000.Configure);
            register(DetonationJuggernaut.DbfId, "CORE_WW_329", DetonationJuggernaut.Configure);
            register(TotemGolem.DbfId, "CORE_AT_052", TotemGolem.Configure);
            register(EarthElemental.DbfId, "CORE_EX1_250", EarthElemental.Configure);
            register(WailingVapor.DbfId, "CORE_WC_042", WailingVapor.Configure);
            register(NoviceZapper.DbfId, "CS3_007", NoviceZapper.Configure);
            register(64432, "SW_108t", FirstFlame.ConfigureSecondFlame);
            register(104462, "WW_329e", DetonationJuggernaut.ConfigureBuff);
            register(71679, "WC_042e", WailingVapor.ConfigureBuff);
        }
    }
}
