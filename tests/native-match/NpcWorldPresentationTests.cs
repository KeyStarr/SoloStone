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
            LadderDefaults();
            UnqualifiedCosmeticsFallBack();
            SkinShapeChecks();
            MulliganSeam();
            OpponentNameComesFromEncounter();
            LadderSettingsDriveNativeMatch();
            WorldPlaytestIds();
            KnobStillOverrides();
            MockCapForWorldPersonality();
            ReactionsStillWaitForIdle();
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

    private static void LadderDefaults()
    {
        var profile = WorldProfile("npc.g.0007", "Tester7", "npc-dragon-warrior", "HERO_01c", "CS2_102_H3", 505);
        var s = NpcPresentationSettings.LadderDefaults(profile);
        Check(s.Ladder && s.Emotes && s.Hover && s.Skin && s.CardBack, "ladder defaults must turn all four capabilities on");
        Check(s.CardBackOverride == null && s.NativeStartingTaunt, "ladder defaults must take the card back from the profile");
        var presentation = new NpcPresentation(profile, s, "ladder", 2, () => 0, () => false, e => true);
        Check(presentation.CardBack(2, 0) == 505 && presentation.CardBack(1, 0) == 0, "card back must apply to the NPC seat only");
        Check(presentation.HeroCardId(5, 5, "HERO_01") == "HERO_01c", "skin must replace the NPC hero card");
        Check(presentation.HeroCardId(6, 5, "HERO_01") == "HERO_01", "skin must not replace another hero");
        bool threw = false;
        try { NpcPresentationSettings.LadderDefaults(null); } catch (ArgumentNullException) { threw = true; }
        Check(threw, "null profile accepted");
        Check(!new NpcPresentationSettings().Ladder && !new NpcPresentationSettings { Emotes = true }.Ladder,
            "hand-built (knob) settings must not claim to be ladder defaults");
    }

    private static void UnqualifiedCosmeticsFallBack()
    {
        var plain = WorldProfile("npc.g.0008", "Plain", "npc-dragon-warrior", "", null, 0);
        var s = NpcPresentationSettings.LadderDefaults(plain);
        Check(s.Emotes && s.Hover && !s.Skin && !s.CardBack, "no skin / no card back must leave both off");
        var skinned = WorldProfile("npc.g.0009", "Skinned", "npc-dragon-warrior", "HERO_01zz", "CS2_102zz", 77);
        s = NpcPresentationSettings.LadderDefaults(skinned, id => false, id => false);
        Check(!s.Skin && !s.CardBack && s.Emotes && s.Hover, "unknown skin/back must fall back, keeping emotes and hover");
        var presentation = new NpcPresentation(skinned, s, "fallback", 2, () => 0, () => false, e => true);
        Check(presentation.HeroCardId(5, 5, "HERO_01") == "HERO_01", "unqualified skin must show the base hero");
        Check(presentation.CardBack(2, 3) == 3, "unqualified card back must keep the default back");
        // The skin and its power travel together: an unknown power drops the skin instead of showing half of it.
        s = NpcPresentationSettings.LadderDefaults(skinned, id => id == "HERO_01zz", id => id == 77);
        Check(!s.Skin && s.CardBack, "unknown skin power must drop the skin");
        s = NpcPresentationSettings.LadderDefaults(skinned, id => true, id => id == 77);
        Check(s.Skin && s.CardBack, "known skin, power and back must be used");
        s = NpcPresentationSettings.LadderDefaults(skinned, id => { throw new InvalidOperationException("dbf"); },
            id => { throw new InvalidOperationException("dbf"); });
        Check(!s.Skin && !s.CardBack, "a failing client lookup must fall back, not throw");
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

    private static void WorldPlaytestIds()
    {
        var notable = WorldProfile("npc.n.ember", "Ember", "npc-pure-paladin", "HERO_04ay", "HERO_04ayhp", 98);
        var generated = WorldProfile("npc.g.0042", "Gen42", "npc-zee-shaman", "", null, 0);
        PlaytestQueue.WorldProfileResolver = null;
        Expect(() => PlaytestQueue.Parse("npc=npc.n.ember"), "world ids must be unknown while the runtime is not installed");
        int consulted = 0;
        PlaytestQueue.WorldProfileResolver = id => { consulted++; return id == notable.Id ? notable : id == generated.Id ? generated : null; };
        var q = PlaytestQueue.Parse("npc=npc.n.ember\nemotes=on\nhover=on\nskin=on\ncardback=12");
        Check(q.Profile == notable && q.Presentation().Emotes && q.Presentation().CardBackOverride == 12, "notable id must resolve with knobs");
        Check(PlaytestQueue.Parse("npc=npc.g.0042").Profile == generated, "generated id must resolve");
        Check(consulted == 2, "resolver must be consulted for world ids");
        Check(PlaytestQueue.Parse("npc=npc.golden.2").Profile == NpcCatalog.All[1] && consulted == 2,
            "golden ids must keep resolving from the catalog without the resolver");
        Expect(() => PlaytestQueue.Parse("npc=npc.n.missing"), "unknown world id accepted");
        Expect(() => PlaytestQueue.Parse("npc=other.n.ember"), "foreign id prefix accepted");
        PlaytestQueue.WorldProfileResolver = id => notable;
        Expect(() => PlaytestQueue.Parse("npc=npc.g.0042"), "resolver returning another NPC must be rejected");
        PlaytestQueue.WorldProfileResolver = id => { throw new InvalidOperationException("boom"); };
        Expect(() => PlaytestQueue.Parse("npc=npc.n.ember"), "throwing resolver must reject, not crash");
        PlaytestQueue.WorldProfileResolver = null;
    }

    private static void SkinShapeChecks()
    {
        Check(NpcPresentationSettings.IsHeroSkin("HERO", "WARRIOR", "WARRIOR"), "same-class hero must be a skin");
        Check(!NpcPresentationSettings.IsHeroSkin("HERO", "PALADIN", "WARRIOR"), "other-class hero accepted as skin");
        Check(!NpcPresentationSettings.IsHeroSkin("MINION", "WARRIOR", "WARRIOR"), "minion accepted as skin");
        Check(!NpcPresentationSettings.IsHeroSkin("HERO_POWER", "WARRIOR", "WARRIOR"), "hero power accepted as skin");
        Check(!NpcPresentationSettings.IsHeroSkin(null, null, "WARRIOR") && !NpcPresentationSettings.IsHeroSkin("HERO", null, null)
            && !NpcPresentationSettings.IsHeroSkin("HERO", "", ""), "unknown card accepted as skin");
        Check(NpcPresentationSettings.IsHeroPower("HERO_POWER") && !NpcPresentationSettings.IsHeroPower("HERO")
            && !NpcPresentationSettings.IsHeroPower(null), "hero power shape check wrong");
        // Wired as ClientBridge does: client-known AND right shape, else the base hero. Fake client definitions.
        var defs = new Dictionary<string, string[]> {
            { "HERO_01c", new[] { "HERO", "WARRIOR" } }, { "CS2_102_H3", new[] { "HERO_POWER", "WARRIOR" } },
            { "HERO_04ay", new[] { "HERO", "PALADIN" } }, { "CS2_182", new[] { "MINION", "NEUTRAL" } } };
        Func<string, string> type = id => defs.ContainsKey(id) ? defs[id][0] : null;
        Func<string, string> cls = id => defs.ContainsKey(id) ? defs[id][1] : null;
        Func<string, string, NpcPresentationSettings> settingsFor = (skin, power) => NpcPresentationSettings.LadderDefaults(
            WorldProfile("npc.g.0011", "Shape", "npc-dragon-warrior", skin, power, 0),
            id => type(id) != null && NpcPresentationSettings.IsHeroSkin(type(id), cls(id), "WARRIOR"), null,
            id => type(id) != null && NpcPresentationSettings.IsHeroPower(type(id)));
        Check(settingsFor("HERO_01c", "CS2_102_H3").Skin, "right-class skin and power must be used");
        Check(!settingsFor("HERO_04ay", "CS2_102_H3").Skin, "wrong-class skin must fall back to the base hero");
        Check(!settingsFor("CS2_182", null).Skin, "non-hero card as skin must fall back");
        Check(!settingsFor("HERO_01zz", null).Skin, "unknown skin must fall back");
        Check(!settingsFor("HERO_01c", "HERO_01c").Skin, "a hero as skin power must drop the skin");
        Check(!settingsFor("HERO_01c", "NOPE").Skin, "unknown skin power must drop the skin");
    }

    private static void Expect(Action action, string message)
    {
        bool rejected = false;
        try { action(); } catch (ArgumentException) { rejected = true; }
        Check(rejected, message);
    }

    private static void KnobStillOverrides()
    {
        // The knob builds hand-made settings (Ladder=false) that ignore every ladder default.
        var golden = NpcCatalog.All[2];
        var q = PlaytestQueue.Parse("npc=npc.golden.3\nemotes=off\nhover=off\nskin=off\ncardback=off");
        var knob = q.Presentation();
        Check(!knob.Ladder && !knob.Emotes && !knob.Hover && !knob.Skin && !knob.CardBack,
            "knob off must stay off even though ladder defaults are on");
        var ladder = NpcPresentationSettings.LadderDefaults(golden);
        Check(ladder.Emotes && ladder.Hover && ladder.Skin, "ladder defaults changed under the knob");
        q = PlaytestQueue.Parse("npc=npc.golden.3\nemotes=on\nskin=off\ncardback=20");
        Check(q.Presentation().CardBackOverride == 20 && q.Presentation().Emotes && !q.Presentation().Skin, "knob values lost");
        var natural = PlaytestQueue.Parse("npc=npc.golden.3").Presentation();
        Check(natural.Emotes && natural.Hover && natural.Skin == ladder.Skin && natural.CardBack == ladder.CardBack,
            "an absent knob must equal the ladder presentation of that NPC");
    }

    private static void MockCapForWorldPersonality()
    {
        // A world archetype may carry several mock rules; the match still allows at most two mocks, never the same kind twice.
        var golden = NpcCatalog.All[2];
        var rules = new[] {
            new EmoteRule(NpcReaction.HumanMisplay, "Oops", 1, 9, mock: true),
            new EmoteRule(NpcReaction.HumanMisplay, "Wow", 1, 9, mock: true) };
        var personality = new Personality(.1, .3, .08, .2, new EmoteProfile(20, 0, rules), .15, .25, 12);
        var profile = WorldProfile("npc.g.0099", "Mocker", golden.DeckId, "", null, 0, personality);
        double now = 0; var sent = new List<string>();
        var presentation = new NpcPresentation(profile, NpcPresentationSettings.LadderDefaults(profile), "mock-cap", 2,
            () => now, () => false, e => { sent.Add(e.Emote); return true; });
        foreach (string kind in new[] { "BadTrade", "IdleMana", "SelfHarm", "BadTrade2" })
            for (int i = 0; i < 10; i++)
            {
                now += 30001;
                presentation.Queue(NpcReaction.HumanMisplay, sent.Count * 1000 + i + kind.Length, kind);
                presentation.Tick(true, false); now += 1201; presentation.Tick(true, false);
            }
        Check(sent.Count == 2, "mock cap must stop at two per match, got " + sent.Count);
    }

    private static void ReactionsStillWaitForIdle()
    {
        // Commit 1968e294: a reaction waits for the native queue to go idle plus a 0.6-1.2 s beat, and stale ones are dropped.
        var profile = WorldProfile("npc.g.0100", "Waiter", "npc-zee-shaman", "", null, 0,
            new Personality(.1, .3, .08, .2, new EmoteProfile(6, 0, new EmoteRule(NpcReaction.OwnBigTurn, "Thanks", 1, 2)), .15, .25, 12));
        double now = 0; var sent = new List<double>();
        var presentation = new NpcPresentation(profile, NpcPresentationSettings.LadderDefaults(profile), "idle", 2,
            () => now, () => false, e => { sent.Add(now); return true; });
        presentation.Queue(NpcReaction.OwnBigTurn, 1);
        now = 3000; presentation.Tick(false, false);
        now = 3100; presentation.Tick(true, false);
        Check(sent.Count == 0, "reaction fired on the first idle frame");
        for (now = 3101; now <= 4400 && sent.Count == 0; now++) presentation.Tick(true, false);
        Check(sent.Count == 1 && sent[0] >= 3700 && sent[0] <= 4300, "reaction beat outside 0.6-1.2 s under ladder defaults");
        presentation.Queue(NpcReaction.OwnBigTurn, 2);
        presentation.Tick(true, true); now += 2000; presentation.Tick(true, true);
        Check(sent.Count == 1, "stale reaction crossed the human-turn boundary");
    }
}
