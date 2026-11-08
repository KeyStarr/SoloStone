using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SabberStoneCore.Model;
using SabberStoneCore.CardSets.Client.Core2026;

namespace SabberStoneCore.CardSets.Client
{
    public static class RebalancedCore2026
    {
        private static readonly Dictionary<int, string> Originals = new Dictionary<int, string>();
        private static readonly IReadOnlyDictionary<int, string> Registry = new ReadOnlyDictionary<int, string>(Originals);
        public static IReadOnlyDictionary<int, string> ByDbfId { get { _ = Cards.Count; return Registry; } }
        internal static void Apply(Dictionary<string, Card> cards)
        {
            var byDbf = Loader.ClientOverlay.Data.Cards.Where(d => !d.ScopedSnapshot).Select(d => cards[d.CardId]).ToDictionary(c => c.AssetId);
            void Register(int dbfId, string originalId, Action<Card> configure)
            {
                var card = byDbf[dbfId];
                if (!cards[originalId].Implemented) throw new InvalidOperationException("Unimplemented base power: " + originalId);
                card.UseBehaviourFrom(cards[originalId]);
                configure(card);
                Originals.Add(dbfId, originalId);
            }
            Register(BallofSpiders.DbfId, "AT_062", BallofSpiders.Configure);
            Register(Astromancer.DbfId, "BOT_256", Astromancer.Configure);
            Register(FinjatheFlyingStar.DbfId, "CFM_344", FinjatheFlyingStar.Configure);
            Register(GreaterHealingPotion.DbfId, "CFM_604", GreaterHealingPotion.Configure);
            Register(GrimestreetOutfitter.DbfId, "CFM_753", GrimestreetOutfitter.Configure);
            Register(ShakutheCollector.DbfId, "CFM_781", ShakutheCollector.Configure);
            Register(DirtyRat.DbfId, "CFM_790", DirtyRat.Configure);
            Register(HolyNova.DbfId, "CS1_112", HolyNova.Configure);
            Register(HolySmite.DbfId, "CS1_130", HolySmite.Configure);
            Register(MarkoftheWild.DbfId, "CS2_009", MarkoftheWild.Configure);
            Register(FireElemental.DbfId, "CS2_042", FireElemental.Configure);
            Register(Hellfire.DbfId, "CS2_062", Hellfire.Configure);
            Register(Assassinate.DbfId, "CS2_076", Assassinate.Configure);
            Register(Consecration.DbfId, "CS2_093", Consecration.Configure);
            Register(HammerofWrath.DbfId, "CS2_094", HammerofWrath.Configure);
            Register(RaidLeader.DbfId, "CS2_122", RaidLeader.Configure);
            Register(WagglePick.DbfId, "DAL_720", WagglePick.Configure);
            Register(Tracking.DbfId, "DS1_184", Tracking.Configure);
            Register(KingMukla.DbfId, "EX1_014", KingMukla.Configure);
            Register(CairneBloodhoof.DbfId, "EX1_110", CairneBloodhoof.Configure);
            Register(FanofKnives.DbfId, "EX1_129", FanofKnives.Configure);
            Register(DefiasRingleader.DbfId, "EX1_131", DefiasRingleader.Configure);
            Register(SI7Agent.DbfId, "EX1_134", SI7Agent.Configure);
            Register(Brightwing.DbfId, "EX1_189", Brightwing.Configure);
            Register(Hex.DbfId, "EX1_246", Hex.Configure);
            Register(LightningStorm.DbfId, "EX1_259", LightningStorm.Configure);
            Register(MortalCoil.DbfId, "EX1_302", MortalCoil.Configure);
            Register(SiphonSoul.DbfId, "EX1_309", SiphonSoul.Configure);
            Register(Doomguard.DbfId, "EX1_310", Doomguard.Configure);
            Register(FlameImp.DbfId, "EX1_319", FlameImp.Configure);
            Register(LordJaraxxus.DbfId, "EX1_323", LordJaraxxus.Configure);
            Register(ArgentProtector.DbfId, "EX1_362", ArgentProtector.Configure);
            Register(TirionFordring.DbfId, "EX1_383", TirionFordring.Configure);
            Register(ArchmageAntonidas.DbfId, "EX1_559", ArchmageAntonidas.Configure);
            Register(Equality.DbfId, "EX1_619", Equality.Configure);
            Register(WitchsApprentice.DbfId, "GIL_531", WitchsApprentice.Configure);
            Register(BlazingInvocation.DbfId, "GIL_836", BlazingInvocation.Configure);
            Register(Coghammer.DbfId, "GVG_059", Coghammer.Configure);
            Register(MusterforBattle.DbfId, "GVG_061", MusterforBattle.Configure);
            Register(SneedsOldShredder.DbfId, "GVG_114", SneedsOldShredder.Configure);
            Register(DrainSoul.DbfId, "ICC_055", DrainSoul.Configure);
            Register(ShadowAscendant.DbfId, "ICC_210", ShadowAscendant.Configure);
            Register(ObsidianStatue.DbfId, "ICC_214", ObsidianStatue.Configure);
            Register(Gnomeferatu.DbfId, "ICC_407", Gnomeferatu.Configure);
            Register(IvoryKnight.DbfId, "KAR_057", IvoryKnight.Configure);
            Register(TheCurator.DbfId, "KAR_061", TheCurator.Configure);
            Register(NetherspiteHistorian.DbfId, "KAR_062", NetherspiteHistorian.Configure);
            Register(Swashburglar.DbfId, "KAR_069", Swashburglar.Configure);
            Register(SilvermoonPortal.DbfId, "KAR_077", SilvermoonPortal.Configure);
            Register(GorillabotA3.DbfId, "LOE_039", GorillabotA3.Configure);
            Register(VulgarHomunculus.DbfId, "LOOT_013", VulgarHomunculus.Configure);
            Register(BladedGauntlet.DbfId, "LOOT_044", BladedGauntlet.Configure);
            Register(SleepyDragon.DbfId, "LOOT_137", SleepyDragon.Configure);
            Register(OakenSummons.DbfId, "LOOT_309", OakenSummons.Configure);
            Register(Voidlord.DbfId, "LOOT_368", Voidlord.Configure);
            Register(WildPyromancer.DbfId, "NEW1_020", WildPyromancer.Configure);
            Register(Doomsayer.DbfId, "NEW1_021", Doomsayer.Configure);
            Register(HammerofTwilight.DbfId, "OG_031", HammerofTwilight.Configure);
            Register(FandralStaghelm.DbfId, "OG_044", FandralStaghelm.Configure);
            Register(CalloftheWild.DbfId, "OG_211", CalloftheWild.Configure);
            Register(HeadhuntersHatchet.DbfId, "TRL_111", HeadhuntersHatchet.Configure);
            Register(HalazzitheLynx.DbfId, "TRL_900", HalazzitheLynx.Configure);
            Register(FirePlumePhoenix.DbfId, "UNG_084", FirePlumePhoenix.Configure);
            Register(PrimordialDrake.DbfId, "UNG_848", PrimordialDrake.Configure);
            Register(JeweledMacaw.DbfId, "UNG_912", JeweledMacaw.Configure);
            Register(SpikeridgedSteed.DbfId, "UNG_952", SpikeridgedSteed.Configure);
            Register(TheBlackKnight.DbfId, "EX1_002", TheBlackKnight.Configure);
            Register(BigGameHunter.DbfId, "EX1_005", BigGameHunter.Configure);
            EffectModules.Apply(cards);
        }
    }
}
