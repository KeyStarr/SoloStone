using System;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    /// <summary>Current CORE-set Standard modules (DruidWarlock slice).</summary>
    internal static class CoreDruidWarlockCards
    {
        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            register(Ulfar.DbfId, "CORE_CATA_006", Ulfar.Configure);
            register(130984, "CORE_CATA_006e", Ulfar.ConfigureDeathrattle);
            register(SpiritBomb.DbfId, "CORE_BOT_222", SpiritBomb.Configure);
            register(CoreMarkOfTheWild.DbfId, "CORE_CS2_009", CoreMarkOfTheWild.Configure);
            register(CoreHellfire.DbfId, "CORE_CS2_062", CoreHellfire.Configure);
            register(Innervate.DbfId, "CORE_EX1_169", Innervate.Configure);
            register(CoreMortalCoil.DbfId, "CORE_EX1_302", CoreMortalCoil.Configure);
            register(CoreSiphonSoul.DbfId, "CORE_EX1_309", CoreSiphonSoul.Configure);
            register(CoreDoomguard.DbfId, "CORE_EX1_310", CoreDoomguard.Configure);
            register(CoreFlameImp.DbfId, "CORE_EX1_319", CoreFlameImp.Configure);
            register(CoreLordJaraxxus.DbfId, "CORE_EX1_323", CoreLordJaraxxus.Configure);
            register(CoreDrainSoul.DbfId, "CORE_ICC_055", CoreDrainSoul.Configure);
            register(CoreGnomeferatu.DbfId, "CORE_ICC_407", CoreGnomeferatu.Configure);
            register(CoreVulgarHomunculus.DbfId, "CORE_LOOT_013", CoreVulgarHomunculus.Configure);
            register(CoreOakenSummons.DbfId, "CORE_LOOT_309", CoreOakenSummons.Configure);
            register(SavageStriker.DbfId, "CORE_TRL_240", SavageStriker.Configure);
            register(Voidlord.DbfId, "CORE_LOOT_368", Voidlord.Configure);
            register(DarkPeddler.DbfId, "CORE_WON_096", DarkPeddler.Configure);
            register(Underking.DbfId, "CORE_RLK_657", Underking.Configure);
            register(BestInShell.DbfId, "CORE_SW_429", BestInShell.Configure);
            register(VibrantSquirrel.DbfId, "CORE_SW_439", VibrantSquirrel.Configure);
            register(CrystalMerchant.DbfId, "CORE_ULD_133", CrystalMerchant.Configure);
            register(ArchwitchWillow.DbfId, "CORE_SCH_181", ArchwitchWillow.Configure);
            register(DemonicAssault.DbfId, "CORE_SW_088", DemonicAssault.Configure);
            register(Riftcleaver.DbfId, "CORE_ULD_165", Riftcleaver.Configure);
            register(70124, "SW_429t", card => card.Implemented = true);
            register(64851, "SW_439t", VibrantSquirrel.ConfigureAcorn);
            register(68116, "SW_439t2", card => card.Implemented = true);
        }
    }
}
