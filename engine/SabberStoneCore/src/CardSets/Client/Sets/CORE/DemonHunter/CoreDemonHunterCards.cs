using System;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    /// <summary>Current CORE-set cards without a scripted legacy original (DemonHunter slice).</summary>
    internal static class CoreDemonHunterCards
    {
        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            register(MythicalTerror.DbfId, "CORE_TTN_866", MythicalTerror.Configure);
            register(IllidariInquisitor.DbfId, "CS3_020", IllidariInquisitor.Configure);
            register(62005, "YOP_001e", IllidariStudies.ConfigureDiscount);
            register(IllidariStudies.DbfId, "CORE_YOP_001", IllidariStudies.Configure);
            register(Netherwalker.DbfId, "CORE_BT_321", Netherwalker.Configure);
            register(CrimsonSigilRunner.DbfId, "CORE_BT_480", CrimsonSigilRunner.Configure);
            register(SpectralSight.DbfId, "CORE_BT_491", SpectralSight.Configure);
            register(EredarDeceptor.DbfId, "CORE_TTN_843", EredarDeceptor.Configure);
            register(101499, "TTN_843t1", EredarDeceptor.ConfigureToken);
            register(Felrattler.DbfId, "CORE_WC_701", Felrattler.Configure);
            register(Tichondrius.DbfId, "CORE_CATA_001", Tichondrius.Configure);
            register(128357, "CORE_CATA_001e", Tichondrius.ConfigureDiscount);
            register(WrathspikeBrute.DbfId, "CORE_BT_510", WrathspikeBrute.Configure);
            register(PriestessOfFury.DbfId, "CORE_BT_493", PriestessOfFury.Configure);
            register(RagingFelscreamer.DbfId, "CORE_BT_416", RagingFelscreamer.Configure);
            register(58171, "BT_416e", RagingFelscreamer.ConfigureDiscount);
            register(Battlefiend.DbfId, "CORE_BT_351", Battlefiend.Configure);
            register(58488, "BT_351e", Battlefiend.ConfigureBuff);
            register(AldrachiWarblades.DbfId, "CORE_BT_921", AldrachiWarblades.Configure);
            register(EyeBeam.DbfId, "CORE_BT_801", EyeBeam.Configure);
        }
    }
}
