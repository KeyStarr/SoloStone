using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

// NPC World v1, lead integration: the real data/npc world installed exactly as the plugin boot does (seam S11, plugin
// EmbeddedJson parser), driving a NativeMatch. Covers the S7/S10 playtest path, restart with a pending match (S5 pin)
// and the failed-boot legacy fallback.
internal static class NpcWorldIntegrationTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception("NpcWorldIntegration: " + message); }
    private static string Data(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "npc", name));
    private static readonly FieldInfo NpcProfileField = typeof(NativeMatch).GetField("npcProfile", BindingFlags.NonPublic | BindingFlags.Instance);

    // Library-only decks (golden decks also live in LocalDecks.NpcDecks and resolve without the world).
    private static bool LibraryOnly(string deckId) => !LocalDecks.NpcDecks.Any(deck => deck.Id == deckId);

    private static NpcWorld Generate()
    {
        NpcWorldJson.Parser = EmbeddedJson.Parse;
        return NpcWorld.Generate(new NpcWorldCatalogs(NpcDeckLibrary.Parse(Data("decks-v1.json")), SkillCatalog.Parse(Data("skills-v1.json")),
            PersonalityCatalog.Parse(Data("personalities-v1.json")), NpcNotables.Parse(Data("notables-v1.json")),
            NpcNameTables.Parse(Data("names-v1.json")), NpcCosmeticPool.Parse(Data("cosmetics-v1.json"))));
    }

    public static void Run()
    {
        Check(NpcProfileField != null, "NativeMatch.npcProfile field missing");
        var world = Generate();
        try
        {
            FailedBootKeepsLegacy(world);
            KnobWorldIdDrivesNativeMatch(world);
            PendingDeckSurvivesRestart(world);
            GuardFallsBackToPlainPilot();
            WorldEncounterRunsTheSkilledBrain(world);
        }
        finally { NpcWorldRuntime.Uninstall(); NpcWorldHooks.Reset(); LocalDecks.ExternalDecks = null; PlaytestQueue.WorldProfileResolver = null; }
        Console.WriteLine("PASS NPC world integration: boot fallback, world playtest knob -> NativeMatch profile, pending deck after restart, "
            + "guarded skill decorator, world encounter on the skilled brain");
    }

    private static void RequireLegacy(string context)
    {
        Check(!NpcWorldRuntime.Installed && NpcWorldRuntime.Matchmaker == null && NpcWorldRuntime.Active == null, context + ": runtime left installed");
        Check(NpcWorldHooks.ProfileForDeck == null && NpcWorldHooks.PlanForId == null && NpcWorldHooks.DecoratePilot == null
            && NpcWorldHooks.MulliganFor == null, context + ": hooks left set");
        Check(LocalDecks.ExternalDecks == null && PlaytestQueue.WorldProfileResolver == null, context + ": client seams left set");
    }

    private static bool Throws(Action action)
    {
        try { action(); return false; } catch (Exception) { return true; }
    }

    private static void FailedBootKeepsLegacy(NpcWorld world)
    {
        NpcWorldRuntime.Uninstall();
        var history = new[] { LocalDecks.All[0].Id, LocalDecks.All[1].Id };
        string legacyNext = LocalDecks.NextOpponent(history).Id;
        var logs = new List<string>();
        Check(!NpcWorldRuntime.TryInstallClient(() => { throw new InvalidDataException("bad npc data"); }, null, logs.Add), "throwing boot reported success");
        Check(logs.Count == 1 && logs[0].StartsWith("npc-world boot failed; using legacy opponents:", StringComparison.Ordinal)
            && logs[0].Contains("bad npc data"), "failed boot must log exactly once: " + string.Join(" | ", logs));
        RequireLegacy("failed boot");
        Check(LocalDecks.NextOpponent(history).Id == legacyNext, "failed boot must keep LocalDecks.NextOpponent");
        Check(Throws(() => LocalDecks.Get(world.Catalogs.Decks.Admitted.First(d => LibraryOnly(d.Id)).Id)), "library decks must be unknown without the world");
        Check(Throws(() => PlaytestQueue.Parse("npc=npc.g.0001")), "world playtest ids must be unknown without the world");
        Check(PlaytestQueue.Parse("npc=npc.golden.1").Profile == NpcCatalog.All[0], "golden playtest ids keep working");

        // A failure after a successful install (re-boot with bad data) never leaves a half-installed world behind.
        Check(NpcWorldRuntime.TryInstallClient(() => world, null, null), "real world must install");
        Check(!NpcWorldRuntime.TryInstallClient(() => null, null, logs.Add), "null world reported success");
        RequireLegacy("failed re-boot");
        // A null world deck library is bad data too.
        Check(!NpcWorldRuntime.TryInstallClient(() => new NpcWorld(1, 1, new NpcWorldCatalogs(null, null, null, new NpcIdentity[0], null, null),
            new NpcIdentity[0]), null, logs.Add), "world without a deck library reported success");
        RequireLegacy("no library");
    }

    private static void KnobWorldIdDrivesNativeMatch(NpcWorld world)
    {
        var logs = new List<string>();
        Check(NpcWorldRuntime.TryInstallClient(() => world, deck => null, logs.Add), "real world must install: " + string.Join(" | ", logs));
        Check(logs.Count == 1 && logs[0].StartsWith("npc-world installed: " + world.All.Count + " NPCs", StringComparison.Ordinal)
            && logs[0].Contains("decks admitted") && logs[0].Contains("Legend field"), "one boot summary line: " + string.Join(" | ", logs));
        Check(LocalDecks.ExternalDecks != null && PlaytestQueue.WorldProfileResolver != null, "client seams installed");
        string rejected = world.Catalogs.Decks.Decks.First(d => d.Admission == "rejected").Id;
        Check(Throws(() => LocalDecks.Get(rejected)), "rejected decks are not served to new matches");

        var notable = world.All.First(n => n.Id.StartsWith("npc.n.", StringComparison.Ordinal));
        var generated = world.All.First(n => n.Id.StartsWith("npc.g.", StringComparison.Ordinal));
        foreach (var npc in new[] { notable, generated })
        {
            var knob = PlaytestQueue.Parse("npc=" + npc.Id + "\nemotes=on\nskin=on");
            Check(knob.Profile.Id == npc.Id && knob.Profile.DisplayName == npc.DisplayName, "knob resolves the world NPC " + npc.Id);
            Check(npc.DeckIds.Contains(knob.Profile.DeckId) && world.Catalogs.Decks.Find(knob.Profile.DeckId).Admitted, "knob plays an admitted deck of the NPC");
            var encounter = NpcWorldRuntime.PlaytestEncounter(knob.Profile, 3, 4242);
            Check(encounter != null && encounter.Reason == "playtest" && ReferenceEquals(encounter.Profile, knob.Profile)
                && encounter.Npc == npc && encounter.Deck.Id == knob.Profile.DeckId && encounter.Skill != null, "playtest encounter wraps the knob profile");
            // LocalLadder.StartSelected order: Begin, then the knob's deck through LocalDecks.Get, then the native match.
            NpcWorldRuntime.Begin(encounter);
            var opponent = knob.Opponent(false);
            Check(opponent.Id == knob.Profile.DeckId && (opponent.Opponent == npc.DisplayName || !LibraryOnly(opponent.Id)),
                "library deck named after the NPC");
            Check(ReferenceEquals(NpcCatalog.ForDeck(opponent.Id), knob.Profile), "S1 resolves the knob profile");
            var match = new NativeMatch((id, packet) => { }, LocalDecks.All[0].Id, opponent.Id, 8427) {
                MatchId = "world-playtest", NativeNpcStartingTaunt = () => false, PresentationSettings = knob.Presentation(),
                NativeNpcUserUI = packet => true };
            match.Start();
            try
            {
                Check(ReferenceEquals(NpcProfileField.GetValue(match), knob.Profile), "NativeMatch NPC profile must be the knob profile: " + npc.Id);
                Check(match.Game.Player2.Name == npc.DisplayName, "engine opponent name must be the world NPC");
            }
            finally { match.StopNpc(); NpcWorldRuntime.End(); }
        }
        Check(NpcWorldRuntime.PlaytestEncounter(NpcCatalog.All[0], 0, 1) == null, "golden knobs keep the legacy (no encounter) path");
        Check(PlaytestQueue.Parse("npc=npc.golden.2").Profile == NpcCatalog.All[1], "golden knob ids resolve from the catalog");
        Check(Throws(() => PlaytestQueue.Parse("npc=npc.n.no-such-npc")) && Throws(() => PlaytestQueue.Parse("npc=npc.g.9999")),
            "unknown world ids are rejected");
        NpcWorldRuntime.Uninstall();
        RequireLegacy("uninstall");
    }

    // Restart: NpcWorldRuntime.Active is gone; a saved, unsettled match still resolves its NPC deck (named for the NPC),
    // even a deck a later library version stopped admitting.
    private static void PendingDeckSurvivesRestart(NpcWorld world)
    {
        var npc = world.All.First(n => n.Id.StartsWith("npc.n.", StringComparison.Ordinal)
            && n.DeckIds.Any(id => LibraryOnly(id) && world.Catalogs.Decks.Find(id)?.Admitted == true));
        string deckId = npc.DeckIds.First(id => LibraryOnly(id) && world.Catalogs.Decks.Find(id)?.Admitted == true);
        string rejected = world.Catalogs.Decks.Decks.First(d => d.Admission == "rejected").Id;
        string pendingDeck = deckId;
        Check(NpcWorldRuntime.TryInstallClient(() => world, id => id == pendingDeck ? npc.Id : null, null), "restart boot");
        Check(NpcWorldRuntime.Active == null, "a restart has no active encounter");
        var deck = LocalDecks.Get(deckId);
        Check(deck.Opponent == npc.DisplayName && deck.Build().Count > 0, "pending deck resolves after restart, named for its NPC");
        pendingDeck = rejected;
        var pinned = LocalDecks.Get(rejected);
        Check(pinned.Id == rejected && pinned.Opponent == npc.DisplayName && pinned.Build().Count > 0, "pending deck stays served when un-admitted");
        Check(LocalDecks.Get(deckId).Opponent == world.Catalogs.Decks.Find(deckId).Name, "an unreferenced deck keeps its library name");
        NpcWorldRuntime.Uninstall();
        Check(NpcWorldRuntime.TryInstallClient(() => world, id => { throw new InvalidOperationException("profile gone"); }, null), "boot with a failing pending lookup");
        Check(LocalDecks.Get(deckId).Id == deckId && Throws(() => LocalDecks.Get(rejected)), "a failing pending lookup pins nothing and breaks nothing");
        NpcWorldRuntime.Uninstall();
    }

    private static readonly FieldInfo NpcPilotField = typeof(NativeMatch).GetField("npcPilot", BindingFlags.NonPublic | BindingFlags.Instance);

    // Live 2026-10-25: every world decision fell to LocalBot because the decorated pilot threw NotImplementedException.
    // The guard keeps the undecorated pilot: on Begin, mid-search (StepNode) and on Decide/Mulligan; it logs once.
    private static void GuardFallsBackToPlainPilot()
    {
        foreach (string where in new[] { "begin", "step", "result", "lethal" })
        {
            var logs = new List<string>();
            var plain = new FakePilot("plain", null);
            var guard = new GuardedSkilledPilot("npc.g.test", new FakePilot("skilled", where), plain, logs.Add);
            var context = new GoldenContext(3, 1, 7, NpcCatalog.All[0]);
            var job = guard.Begin(null, context);
            var scheduler = new NodeScheduler(1000);
            var lethal = (job as INpcLethalSearch)?.LethalSearch;
            if (lethal != null) { var l = new NodeScheduler(100); l.Enqueue(lethal); while (l.Step(1) == NodeStepResult.Running) { } }
            scheduler.Enqueue(job);
            while (!job.IsDone && scheduler.Step(1) == NodeStepResult.Running) { }
            var result = job.Result;
            Check(guard.Disabled && result.Value == plain.Value && result.Nodes == FakePilot.Nodes,
                where + ": the decision must come from the plain pilot, got value " + result.Value + " nodes=" + result.Nodes);
            Check(logs.Count == 1 && logs[0].Contains("NotImplementedException") && logs[0].Contains("npc.g.test"), where + ": logged once: " + string.Join(" | ", logs));
            var next = guard.Begin(null, new GoldenContext(3, 2, 8, NpcCatalog.All[0]));
            while (!next.IsDone) next.StepNode();
            Check(next.Result.Value == plain.Value && logs.Count == 1, where + ": later decisions stay on the plain pilot, silently");
            Check(guard.Decide(null, context).Value == plain.Value, where + ": Decide uses the plain pilot");
            Check(guard.Mulligan(null, context).SequenceEqual(new[] { 42 }), where + ": Mulligan uses the plain pilot");
        }
        // A working decorator stays in charge; a non-NotImplemented failure after first use propagates as before.
        var ok = new GuardedSkilledPilot("npc.g.ok", new FakePilot("skilled", null), new FakePilot("plain", null));
        var first = ok.Begin(null, new GoldenContext(1, 1, 1, NpcCatalog.All[0]));
        while (!first.IsDone) first.StepNode();
        Check(!ok.Disabled && first.Result.Value == 2, "working decorator decides");
        // Construction failure (DecoratePilot) and the S3 seam (NpcPilots.Create) both fall back to the plain golden pilot.
        NpcWorldHooks.DecoratePilot = (p, inner) => { throw new NotImplementedException(); };
        try { Check(NpcPilots.Create(NpcCatalog.All[0]) is GoldenPilot, "throwing decorator -> plain golden pilot"); }
        finally { NpcWorldHooks.DecoratePilot = null; }
    }

    private sealed class FakePilot : IFrameSlicedGoldenPilot
    {
        internal const int Nodes = 3;
        private readonly string name, fail;
        internal FakePilot(string name, string fail) { this.name = name; this.fail = fail; }
        internal double Value => name == "plain" ? 1 : 2;
        public IGoldenDecisionJob Begin(FairBotState view, GoldenContext context)
        {
            if (fail == "begin") throw new NotImplementedException();
            return new FakeJob(this);
        }
        public GoldenDecision Decide(FairBotState view, GoldenContext ctx)
        { if (fail != null) throw new NotImplementedException(); return new GoldenDecision(null, Value, Nodes, 1, "Nodes", null, null); }
        public IReadOnlyList<int> Mulligan(IGoldenMulliganView view, GoldenContext ctx)
        { if (fail != null) throw new NotImplementedException(); return new[] { 42 }; }
        private sealed class FakeJob : IGoldenDecisionJob, INpcLethalSearch
        {
            private readonly FakePilot pilot; private int nodes;
            private readonly LethalJob lethal;
            internal FakeJob(FakePilot pilot) { this.pilot = pilot; lethal = new LethalJob(pilot); }
            public bool IsDone => nodes >= Nodes;
            public void StepNode() { if (pilot.fail == "step") throw new NotImplementedException(); nodes++; }
            public GoldenDecision Result
            {
                get
                {
                    if (pilot.fail == "result") throw new NotImplementedException();
                    return new GoldenDecision(null, pilot.Value, nodes, 1, "Nodes", null, null, "search");
                }
            }
            public string LethalResult => "none";
            public bool PlanReused => false;
            public INodeJob LethalSearch => lethal;
        }
        private sealed class LethalJob : INodeJob
        {
            private readonly FakePilot pilot; private bool done;
            internal LethalJob(FakePilot pilot) { this.pilot = pilot; }
            public bool IsDone => done;
            public void StepNode() { if (pilot.fail == "lethal") throw new NotImplementedException(); done = true; }
        }
    }

    // A ladder-style world encounter in a NativeMatch decides on the golden search (nodes > 0) through the skill
    // decorator, never on the LocalBot fallback, and names the native opponent after the NPC.
    private static void WorldEncounterRunsTheSkilledBrain(NpcWorld world)
    {
        Check(NpcPilotField != null, "NativeMatch.npcPilot field missing");
        Check(NpcWorldRuntime.TryInstallClient(() => world, deck => null, null), "world install");
        try
        {
            NpcEncounter encounter = null;
            foreach (var npc in world.All.Where(n => n.Id.StartsWith("npc.g.", StringComparison.Ordinal)).Take(400))
            {
                var profile = NpcWorldRuntime.PlaytestProfile(npc.Id);
                var candidate = profile == null ? null : NpcWorldRuntime.PlaytestEncounter(profile, 3, 9001);
                if (candidate != null && (candidate.Skill.Mistakes.Count > 0 || candidate.Skill.Temperature > 0)) { encounter = candidate; break; }
            }
            Check(encounter != null, "a generated NPC with a non-elite skill tier");
            NpcWorldRuntime.Begin(encounter);
            var packets = new List<object>(); var log = new List<string>();
            double now = 0;
            var match = new NativeMatch((id, packet) => packets.Add(packet), LocalDecks.All[0].Id, encounter.Deck.Id, 8427) {
                MatchId = "world-ladder", NativeNpcStartingTaunt = () => false, NativeNpcUserUI = packet => true,
                PresentationSettings = NpcPresentationSettings.LadderDefaults(encounter.Profile), NpcTurnNodes = 3000,
                SynchronousNpc = true, NpcClock = () => now, NativeNpcIdle = () => true, BotTrace = log.Add };
            match.Start();
            try
            {
                Check(match.OpponentName == encounter.Npc.DisplayName && match.Game.Player2.Name == encounter.Npc.DisplayName,
                    "native opponent name is the NPC DisplayName");
                var pilot = NpcPilotField.GetValue(match) as GuardedSkilledPilot;
                Check(pilot != null && pilot.Skilled is SkilledPilot, "world pilot is the guarded SkilledPilot");
                match.SendChoices(packets.OfType<PegasusGame.EntityChoices>().Last(p => p.PlayerId == 1).Id, new List<int>());
                for (int turn = 0; turn < 2 && match.Game.State != SabberStoneCore.Enums.State.COMPLETE; turn++)
                {
                    var options = packets.OfType<PegasusGame.AllOptions>().Last();
                    match.SendOption(options.Id, options.Options.FindIndex(o => o.Type_ == PegasusGame.Option.Type.END_TURN), 0, -1, 0);
                    for (int i = 0; match.NpcRunner?.Active == true && i < 10000; i++) { now += 100; match.TickNpc(); }
                    Check(match.NpcRunner?.Active != true, "world NPC runner stuck");
                }
                var decisions = log.Where(l => l.StartsWith("npc=" + encounter.Profile.Id + " turn=", StringComparison.Ordinal)).ToList();
                Check(decisions.Count > 0 && !log.Any(l => l.Contains("NotImplementedException")) && !pilot.Disabled,
                    "world decisions on the skilled brain: " + string.Join(" | ", log.Take(6)));
                Check(decisions.Any(l => System.Text.RegularExpressions.Regex.IsMatch(l, @" nodes=[1-9]\d* ") && l.Contains("fallback=none")),
                    "a world decision searched (nodes > 0) without fallback: " + string.Join(" | ", decisions.Take(6)));
                Check(log.Any(l => l.Contains(" turn-summary ")), "per-turn summary logged");
            }
            finally { match.StopNpc(); NpcWorldRuntime.End(); }
        }
        finally { NpcWorldRuntime.Uninstall(); }
    }
}
