using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Enums;

// NPC World v1, WP6: mulligan seam S4, ladder-default presentation S8 (settings half), playtest world ids S10.
// The ClientBridge half of S8 is a one-line choice (playtest knob first, else LadderDefaults), covered by `LadderDefaults`
// here plus the knob checks below; it needs the Unity client to run.
internal static class NpcWorldPresentationTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception("NpcWorldPresentation: " + message); }

    public static void Run()
    {
        NpcWorldHooks.Reset();
        try
        {
            MulliganSeam();
            OpponentNameComesFromEncounter();
            LadderSettingsDriveNativeMatch();
        }
        finally { NpcWorldHooks.Reset(); PlaytestQueue.WorldProfileResolver = null; }
        Console.WriteLine("PASS NPC world presentation: mulligan seam, ladder defaults, cosmetic fallbacks, world playtest ids");
    }

    private static NpcProfile WorldProfile(string id, string name, string deckId, string skin, string power, int cardBack,
        Personality personality = null)
    {
        var golden = NpcCatalog.All[0];
        return new NpcProfile(id, 1, name, deckId, skin, cardBack, personality ?? golden.Personality, golden.Budget,
            golden.PlanId, power);
    }

    private sealed class ScriptedMulligan : MulliganPolicy
    {
        private readonly Func<MulliganView, IReadOnlyList<int>> keep;
        internal ScriptedMulligan(Func<MulliganView, IReadOnlyList<int>> keep) { this.keep = keep; }
        public override IReadOnlyList<int> Keep(MulliganView view) => keep(view);
    }

    private sealed class RecordingMulligan : MulliganPolicy
    {
        internal readonly List<MulliganView> Views = new List<MulliganView>();
        internal bool KeepEverything = true;
        public override IReadOnlyList<int> Keep(MulliganView view)
        {
            Views.Add(view);
            return KeepEverything ? view.Offered.Where(c => c.CardId != "GAME_005").Select(c => c.EntityId).ToArray() : new int[0];
        }
    }

    private static NativeMatch StartPlain(string deckId, NpcPresentationSettings settings, List<object> packets, List<UserUI> ui = null)
    {
        var deck = LocalDecks.Get(deckId);
        var match = new NativeMatch((id, packet) => packets.Add(packet), deck.Id, deck.Id, 8427) {
            MatchId = "world-presentation", NativeNpcStartingTaunt = () => false, PresentationSettings = settings,
            NativeNpcUserUI = packet => { ui?.Add(packet); return true; }
        };
        match.Start();
        return match;
    }

    private static void MulliganSeam()
    {
        var golden = NpcCatalog.All[1];
        // No hook: today's DeckPlanMulligan policy, byte for byte.
        var baseline = StartPlain(golden.DeckId, new NpcPresentationSettings(), new List<object>());
        var baselineHand = baseline.Game.Player2.HandZone.Select(c => c.Id).ToArray();
        Check(NativeMatch.BotMulliganPolicy(golden) is DeckPlanMulligan, "null hook must use the plan mulligan");
        var recorder = new RecordingMulligan();
        NpcProfile seenProfile = null; DeckPlan seenPlan = null;
        NpcWorldHooks.MulliganFor = (profile, plan) => { seenProfile = profile; seenPlan = plan; return recorder; };
        var match = StartPlain(golden.DeckId, new NpcPresentationSettings(), new List<object>());
        Check(recorder.Views.Count == 1, "hook policy must be consulted exactly once at match start");
        Check(seenProfile == golden && seenPlan == DeckPlans.ForId(golden.PlanId), "hook must get the encounter profile and its plan");
        var offered = recorder.Views[0].Offered.Where(c => c.CardId != "GAME_005").Select(c => c.EntityId).ToArray();
        var hand = match.Game.Player2.HandZone.Select(c => c.Id).ToArray();
        Check(offered.Length > 0 && offered.All(hand.Contains), "keep-all hook policy must keep the whole opening hand");
        Check(recorder.Views[0].OwnDeck.SequenceEqual(LocalDecks.Get(golden.DeckId).CardIds),
            "mulligan view must carry only the bot's own deck list");
        // A hook that returns null or throws must never break the match.
        NpcWorldHooks.MulliganFor = (profile, plan) => null;
        Check(NativeMatch.BotMulliganPolicy(golden) is DeckPlanMulligan, "null policy must fall back");
        NpcWorldHooks.MulliganFor = (profile, plan) => { throw new InvalidOperationException("hook"); };
        Check(NativeMatch.BotMulliganPolicy(golden) is DeckPlanMulligan, "throwing hook must fall back");
        var fallback = StartPlain(golden.DeckId, new NpcPresentationSettings(), new List<object>());
        Check(fallback.Game.Player2.HandZone.Select(c => c.Id).SequenceEqual(baselineHand), "fallback mulligan must equal the baseline");
        // A hook whose policy throws inside Keep, or returns an invalid keep list, must not abort the match start.
        var bad = new Dictionary<string, Func<MulliganView, IReadOnlyList<int>>> {
            { "throwing Keep", v => { throw new InvalidOperationException("keep"); } },
            { "null list", v => null },
            { "unoffered id", v => new[] { 987654 } },
            { "duplicate id", v => { int id = v.Offered.First(c => c.CardId != "GAME_005").EntityId; return new[] { id, id }; } },
            { "coin kept", v => v.Offered.Where(c => c.CardId == "GAME_005").Select(c => c.EntityId).ToArray() } };
        foreach (var pair in bad)
        {
            var scripted = new ScriptedMulligan(pair.Value);
            NpcWorldHooks.MulliganFor = (profile, plan) => scripted;
            var started = StartPlain(golden.DeckId, new NpcPresentationSettings(), new List<object>());
            Check(started.Game.Player2.HandZone.Select(c => c.Id).SequenceEqual(baselineHand),
                pair.Key + " must fall back to the plan mulligan at match start");
            Check(started.Game.Player2.Choice == null, pair.Key + " left the NPC stuck in the mulligan");
        }
        NpcWorldHooks.Reset();
    }

    private static void OpponentNameComesFromEncounter()
    {
        var golden = NpcCatalog.All[0];
        var deck = LocalDecks.Get(golden.DeckId);
        Check(NativeMatch.OpponentDisplayName(golden, deck) == golden.DisplayName, "name plate must show the encounter NPC");
        Check(NativeMatch.OpponentDisplayName(null, deck) == deck.Opponent, "no NPC must use the deck's opponent name");
        var renamed = WorldProfile("npc.n.renamed", "Renamed Notable", golden.DeckId, "", null, 0);
        Check(NativeMatch.OpponentDisplayName(renamed, deck) == "Renamed Notable", "world NPC name must win over the deck's");
        var started = StartPlain(golden.DeckId, new NpcPresentationSettings(), new List<object>());
        Check(started.Game.Player2.Name == golden.DisplayName, "engine opponent name must be the NPC name");
        // Once the world's profile hook (seam S1) is merged, a hook profile for the deck must name the opponent.
        NpcWorldHooks.ProfileForDeck = id => id == golden.DeckId ? renamed : null;
        if (NpcCatalog.ForDeck(golden.DeckId) == renamed)
        {
            var named = StartPlain(golden.DeckId, NpcPresentationSettings.LadderDefaults(renamed), new List<object>());
            Check(named.Game.Player2.Name == "Renamed Notable", "world encounter must name the opponent");
        }
        NpcWorldHooks.Reset();
    }

    private static void LadderSettingsDriveNativeMatch()
    {
        var golden = NpcCatalog.All[0];
        var settings = NpcPresentationSettings.LadderDefaults(golden, id => true, id => id == 505);
        Check(settings.Skin && !settings.CardBack, "golden profile has no card back of its own (505 comes from the knob)");
        var packets = new List<object>(); var ui = new List<UserUI>();
        var match = StartPlain(golden.DeckId, settings, packets, ui);
        var heroes = packets.OfType<PowerHistory>().SelectMany(p => p.List).Where(p => p.HasFullEntity).Select(p => p.FullEntity).ToArray();
        Check(heroes.Last(e => e.Entity == match.Game.Player2.Hero.Id).Name == golden.HeroSkin, "ladder defaults must apply the hero skin");
        // Skin off by qualification: the match runs on the base hero and base hero power.
        var packets2 = new List<object>();
        var plain = StartPlain(golden.DeckId, NpcPresentationSettings.LadderDefaults(golden, id => false, id => false), packets2);
        var heroes2 = packets2.OfType<PowerHistory>().SelectMany(p => p.List).Where(p => p.HasFullEntity).Select(p => p.FullEntity).ToArray();
        Check(heroes2.Last(e => e.Entity == plain.Game.Player2.Hero.Id).Name == plain.Game.Player2.Hero.Card.Id,
            "unqualified skin must show the base hero");
        Check(heroes2.Last(e => e.Entity == plain.Game.Player2.Hero.HeroPower.Id).Name
            == NativeMatch.TransportCardId(plain.Game.Player2.Hero.HeroPower.Card.Id), "unqualified skin must keep the base hero power");
        // A world profile with a card back: shown on the NPC seat only.
        var backed = new NpcProfile(golden.Id, golden.Version, golden.DisplayName, golden.DeckId, golden.HeroSkin, 505,
            golden.Personality, golden.Budget, golden.PlanId, golden.HeroSkinPower);
        var presentation = new NpcPresentation(backed, NpcPresentationSettings.LadderDefaults(backed, id => true, id => id == 505),
            "back", 2, () => 0, () => false, e => true);
        Check(presentation.CardBack(2, 0) == 505 && presentation.CardBack(1, 0) == 0, "ladder card back must apply to the NPC seat");
        match.StopNpc(); plain.StopNpc();
    }

    private static void Expect(Action action, string message)
    {
        bool rejected = false;
        try { action(); } catch (ArgumentException) { rejected = true; }
        Check(rejected, message);
    }

}
