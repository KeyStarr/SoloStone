using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PegasusGame;

// Rivals-lite (backlog #9): the NPC's memory of the player changes only the native name plate and the emote choice.
internal static class NpcMemoryTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception("NpcMemory: " + message); }
    private static string Data(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "npc", name));

    private static readonly NpcMemory Rival = new NpcMemory(0, 2, 0, NpcLastMeeting.PlayerLost, true);
    private static readonly NpcMemory Respect = new NpcMemory(3, 3, 0, NpcLastMeeting.PlayerWon, false);
    private static readonly NpcMemory Grudge = new NpcMemory(1, 0, 0, NpcLastMeeting.PlayerWon, false);
    // 1-1 after a loss: 0-1 would be a rival now (losses > wins), so the smug fixture must be a real non-rival record.
    private static readonly NpcMemory Smug = new NpcMemory(1, 1, 0, NpcLastMeeting.PlayerLost, false);
    private static readonly NpcMemory Even = new NpcMemory(1, 0, 1, NpcLastMeeting.Tied, false);

    public static void Run()
    {
        Plate();
        var catalog = Personalities();
        EmoteTable(catalog);
        PresentationGating(catalog);
        ConcessionWaitsForFarewell();
        string fairness = NoGameplayEffect();
        Console.WriteLine("PASS NPC memory: name plate W-L/ties/rival, MatchStart and beat-rival emote table for all "
            + catalog.Archetypes.Count + " archetypes, squelch/settings/budget/gap gating, timing identical with memory, concession waits for the rival farewell, no gameplay effect (" + fairness + ")");
    }

    private static void Plate()
    {
        Check(NpcMemoryText.Plate("Bob", null) == "Bob", "first meeting plain");
        Check(NpcMemoryText.Plate("Bob", new NpcMemory(2, 1, 0, NpcLastMeeting.PlayerWon, false)) == "Bob  2–1", "repeat W-L");
        Check(NpcMemoryText.Plate("Bob", new NpcMemory(1, 1, 2, NpcLastMeeting.Tied, false)) == "Bob  1–1–2", "ties appended");
        Check(NpcMemoryText.Plate("Bob", Rival) == "Bob · Rival 0–2", "rival plate: " + NpcMemoryText.Plate("Bob", Rival));
        Check(NpcMemoryText.Plate("Bob", new NpcMemory(1, 3, 1, NpcLastMeeting.PlayerLost, true)) == "Bob · Rival 1–3–1", "rival with ties");
        Check(NpcMemoryText.Plate(null, Rival) == null && NpcMemoryText.Plate("", Rival) == "", "no name stays empty");
        // The ledger seam, rival rule (losses > wins): 0-1 is already a rival; 1-1 is not; 1-2 is a rival again.
        var zeroOne = NativeMatch.MemoryFrom(new LocalNpcRecord(0, 1, 0, "Lost"));
        Check(zeroOne.Rival && NpcMemoryText.Plate("Bob", zeroOne) == "Bob · Rival 0–1", "0-1 is a rival: " + NpcMemoryText.Plate("Bob", zeroOne));
        var evenOne = NativeMatch.MemoryFrom(new LocalNpcRecord(1, 1, 0, "Lost"));
        Check(!evenOne.Rival && NpcMemoryText.Plate("Bob", evenOne) == "Bob  1–1", "1-1 is not a rival: " + NpcMemoryText.Plate("Bob", evenOne));
        var oneTwo = NativeMatch.MemoryFrom(new LocalNpcRecord(1, 2, 0, "Lost"));
        Check(oneTwo.Rival && NpcMemoryText.Plate("Bob", oneTwo) == "Bob · Rival 1–2", "1-2 is a rival again");
        bool threw = false;
        try { new NpcMemory(0, 0, 0, NpcLastMeeting.Tied, false); } catch (ArgumentException) { threw = true; }
        Check(threw, "memory without meetings accepted");
        // The ledger seam: an empty record is a first meeting; Abandoned/Lost read as the NPC's win.
        Check(NativeMatch.MemoryFrom(LocalNpcRecord.None) == null && NativeMatch.MemoryFrom(null) == null, "no record = first meeting");
        var lost = NativeMatch.MemoryFrom(LocalNpcRecord.Of(new[] {
            new LocalMatchRecord { NpcId = "n", Result = "Won" }, new LocalMatchRecord { NpcId = "n", Result = "Lost", Conceded = true },
            new LocalMatchRecord { NpcId = "n", Result = "Abandoned" } }, "n"));
        Check(lost.Wins == 1 && lost.Losses == 2 && lost.Ties == 0 && lost.LastMeeting == NpcLastMeeting.PlayerLost && lost.Rival, "record -> memory");
        var tied = NativeMatch.MemoryFrom(new LocalNpcRecord(2, 2, 1, "Tied"));
        Check(tied.LastMeeting == NpcLastMeeting.Tied && !tied.Rival && tied.Respect, "tied, even, respected");
        Check(NativeMatch.MemoryFrom(new LocalNpcRecord(1, 0, 0, "Won")).LastMeeting == NpcLastMeeting.PlayerWon, "player won last");
    }

    private static PersonalityCatalog Personalities()
    {
        NpcWorldJson.Parser = EmbeddedJson.Parse;
        return PersonalityCatalog.Parse(Data("personalities-v1.json"));
    }

    private static void EmoteTable(PersonalityCatalog catalog)
    {
        // archetype -> rival, respect, grudge, smug, even greeting; beat-rival farewell ("-" = none / normal rules).
        var expected = new Dictionary<string, string[]> {
            { "pro",       new[] { "Greetings", "WellPlayed", "Greetings", "Greetings", "Greetings", "WellPlayed" } },
            { "veteran",   new[] { "Greetings", "WellPlayed", "Greetings", "Greetings", "Greetings", "WellPlayed" } },
            { "gambler",   new[] { "Threaten",  "WellPlayed", "Threaten",  "Thanks",    "Greetings", "Wow,WellPlayed" } },
            { "grinder",   new[] { "Threaten",  "WellPlayed", "Threaten",  "Greetings", "Greetings", "WellPlayed" } },
            { "showboat",  new[] { "Threaten",  "WellPlayed", "Threaten",  "Thanks",    "Greetings", "Wow,WellPlayed" } },
            { "silent",    new[] { "-",         "-",          "-",         "-",         "-",         "-" } },
            { "friendly",  new[] { "Greetings", "WellPlayed", "Greetings", "Greetings", "Greetings", "WellPlayed" } },
            { "professor", new[] { "Greetings", "WellPlayed", "Greetings", "Greetings", "Greetings", "WellPlayed" } },
            { "speedster", new[] { "Greetings", "WellPlayed", "Greetings", "Greetings", "Greetings", "WellPlayed" } },
            { "chaotic",   new[] { "Wow",       "WellPlayed", "Greetings", "Wow",       "Greetings", "Wow,WellPlayed" } },
        };
        Check(catalog.Archetypes.Select(a => a.Id).OrderBy(id => id).SequenceEqual(expected.Keys.OrderBy(id => id)),
            "table covers every archetype: " + string.Join(",", catalog.Archetypes.Select(a => a.Id)));
        foreach (var archetype in catalog.Archetypes)
        {
            var emotes = archetype.Emotes;
            var set = new HashSet<string>(emotes.Rules.Select(r => r.Emote));
            string G(NpcMemory memory) => NpcMemoryEmotes.Greeting(emotes, memory) ?? "-";
            string B(NpcMemory memory) => NpcMemoryEmotes.BeatRival(emotes, memory).Count == 0 ? "-" : string.Join(",", NpcMemoryEmotes.BeatRival(emotes, memory));
            var actual = new[] { G(Rival), G(Respect), G(Grudge), G(Smug), G(Even), B(Rival) };
            Check(actual.SequenceEqual(expected[archetype.Id]), archetype.Id + " table: " + string.Join("|", actual));
            Check(G(null) == "-" && B(null) == "-" && B(Smug) == "-" && B(Respect) == "-", archetype.Id + " memory-less or non-rival");
            Check(actual.SelectMany(a => a.Split(',')).All(e => e == "-" || set.Contains(e) && PersonalityCatalog.BotEmotes.Contains(e)),
                archetype.Id + " uses an emote outside its own set");
            if (archetype.Warm) Check(!actual.Any(a => a.Contains("Threaten")) && B(Rival) == "WellPlayed", archetype.Id + " warm never threatens");
        }
    }

    private static NpcProfile Profile(EmoteProfile emotes)
    {
        var golden = NpcCatalog.All[0];
        var p = golden.Personality;
        return new NpcProfile("npc.t.memory", 1, "Memo", golden.DeckId, null, 0,
            new Personality(p.PreTurnMinSeconds, p.PreTurnMaxSeconds, p.ActionMinSeconds, p.ActionMaxSeconds, emotes,
                p.ReplyBlendBeta, p.ConcessionWillingness, p.ConcedeFromTurn), golden.Budget, golden.PlanId);
    }

    private sealed class Probe
    {
        internal double Now;
        internal bool Squelched;
        internal readonly List<string> Sent = new List<string>();
        internal NpcPresentation P;
        internal Probe(NpcProfile profile, NpcMemory memory, string matchId = "memory", bool emotes = true)
        {
            P = new NpcPresentation(profile, new NpcPresentationSettings { Emotes = emotes }, matchId, 2, () => Now, () => Squelched,
                e => { if (e.Emote != null) Sent.Add(e.Emote); return true; }, memory);
        }
        internal void Fire(NpcReaction reaction, int index, bool npcLoses = false)
        {
            P.Queue(reaction, index, delayMilliseconds: 0, npcLoses: npcLoses);
            for (int i = 0; i < 40; i++) { P.Tick(true, false); Now += 100; }
        }
    }

    private static void PresentationGating(PersonalityCatalog catalog)
    {
        var gambler = Profile(catalog.Find("gambler").Emotes);
        // Gambler's MatchStart rules always fire (Greetings at chance 1 after Threaten .6), so memory always swaps it.
        for (int i = 0; i < 20; i++)
        {
            var run = new Probe(gambler, Smug, "memory-" + i);
            run.Fire(NpcReaction.MatchStart, 0);
            Check(run.Sent.SequenceEqual(new[] { "Thanks" }), "smug gambler greets with Thanks: " + string.Join(",", run.Sent));
        }
        var silent = new Probe(Profile(catalog.Find("silent").Emotes), Rival);
        silent.Fire(NpcReaction.MatchStart, 0);
        Check(silent.Sent.Count == 0, "silent stays silent at match start");
        silent.Now += 70000; silent.Fire(NpcReaction.Concession, 1, npcLoses: true);
        Check(silent.Sent.Count <= 1 && silent.Sent.All(e => e == "WellPlayed"), "silent keeps its own farewell rule");

        var squelched = new Probe(gambler, Rival) { Squelched = true };
        squelched.Fire(NpcReaction.MatchStart, 0);
        Check(squelched.Sent.Count == 0, "squelch silences the memory greeting");
        var off = new Probe(gambler, Rival, emotes: false);
        off.Fire(NpcReaction.MatchStart, 0);
        Check(off.Sent.Count == 0, "emote setting off silences the memory greeting");

        // Rival farewell on the NPC's own concession: Wow, then WellPlayed one beat later in the same reaction.
        var rival = new Probe(gambler, Rival);
        rival.Fire(NpcReaction.MatchStart, 0);
        Check(rival.Sent.SequenceEqual(new[] { "Threaten" }), "rival gambler threatens");
        rival.Now += 20000;
        rival.P.Queue(NpcReaction.Concession, 1, delayMilliseconds: 0, npcLoses: true);
        rival.P.Tick(true, false);
        Check(rival.Sent.Last() == "Wow" && rival.P.FarewellFollowUpPending, "first beat-rival emote, follow-up pending");
        for (int i = 0; i < 40; i++) { rival.Now += 100; rival.P.Tick(true, false); }
        Check(rival.Sent.SequenceEqual(new[] { "Threaten", "Wow", "WellPlayed" }) && !rival.P.FarewellFollowUpPending,
            "beat-rival sequence: " + string.Join(",", rival.Sent));

        // The human's concession is not a beaten rival (normal rules: WellPlayed .5 or nothing).
        for (int i = 0; i < 20; i++)
        {
            var human = new Probe(gambler, Rival, "human-concede-" + i);
            human.Fire(NpcReaction.Concession, 1);
            Check(!human.Sent.Contains("Wow") && !human.P.FarewellFollowUpPending, "human concession uses the normal farewell");
        }
        // Warm / plain archetypes: WellPlayed with certainty.
        for (int i = 0; i < 20; i++)
        {
            var veteran = new Probe(Profile(catalog.Find("veteran").Emotes), Rival, "veteran-" + i);
            veteran.Fire(NpcReaction.Concession, 1, npcLoses: true);
            Check(veteran.Sent.SequenceEqual(new[] { "WellPlayed" }), "warm rival farewell is WellPlayed");
        }

        // Budget: a one-emote budget spent on the greeting leaves no farewell; the gap still applies between reactions.
        EmoteProfile Tight() => new EmoteProfile(1, 0, new EmoteRule(NpcReaction.MatchStart, "Greetings", 1),
            new EmoteRule(NpcReaction.Concession, "WellPlayed", 1), new EmoteRule(NpcReaction.HumanMisplay, "Wow", 1, mock: true));
        var tight = new Probe(Profile(Tight()), Rival);
        tight.Fire(NpcReaction.MatchStart, 0);
        tight.Fire(NpcReaction.Concession, 1, npcLoses: true);
        Check(tight.Sent.SequenceEqual(new[] { "Wow" }), "budget: " + string.Join(",", tight.Sent));
        var gap = new Probe(gambler, Rival);
        gap.Fire(NpcReaction.MatchStart, 0);
        gap.Fire(NpcReaction.Concession, 1, npcLoses: true);
        Check(gap.Sent.SequenceEqual(new[] { "Threaten" }), "gap blocks a farewell right after the greeting");
        var two = new Probe(Profile(Tight()), Rival);
        two.Fire(NpcReaction.Concession, 1, npcLoses: true);
        Check(two.Sent.SequenceEqual(new[] { "Wow" }) && !two.P.FarewellFollowUpPending, "follow-up respects the budget: " + string.Join(",", two.Sent));

        SameTimingWithMemory(catalog);
    }

    // Memory never changes WHEN the NPC emotes or waits: BotDoomed and every non-MatchStart trigger are identical with and
    // without rival memory, and the MatchStart greeting is swapped 1:1 (sent exactly when the normal rules send).
    private static void SameTimingWithMemory(PersonalityCatalog catalog)
    {
        int compared = 0;
        foreach (var archetype in catalog.Archetypes)
            for (int m = 0; m < 25; m++)
            {
                string id = "timing-" + archetype.Id + "-" + m;
                var traces = new[] { (NpcMemory)null, Rival, Smug, Respect }.Select(memory => Trace(Profile(archetype.Emotes), memory, id)).ToArray();
                foreach (var t in traces.Skip(1))
                {
                    Check(t.Waits == traces[0].Waits, archetype.Id + " WaitingForReaction differs with memory (" + id + ")");
                    Check(t.Times == traces[0].Times, archetype.Id + " emote timing differs with memory (" + id + ")");
                    Check(t.Later == traces[0].Later, archetype.Id + " BotDoomed/turn emotes differ with memory: " + t.Later + " vs " + traces[0].Later);
                }
                compared++;
            }
        Check(compared == catalog.Archetypes.Count * 25, "timing comparisons ran");
    }

    private sealed class Timing { internal string Waits, Times, Later; }
    private static Timing Trace(NpcProfile profile, NpcMemory memory, string matchId)
    {
        double now = 0; var sent = new List<(double At, string Emote, bool Start)>(); bool start = true;
        var p = new NpcPresentation(profile, new NpcPresentationSettings { Emotes = true }, matchId, 2, () => now, () => false,
            e => { if (e.Emote != null) sent.Add((now, e.Emote, start)); return true; }, memory);
        var waits = new System.Text.StringBuilder();
        void Run(double until) { for (; now < until; now += 50) { p.Tick(true, false); waits.Append(p.WaitingForReaction ? '1' : '0'); } }
        p.Queue(NpcReaction.MatchStart, 0);
        Run(4000); start = false;
        p.Queue(NpcReaction.OwnBigTurn, 1); Run(8000);
        p.Queue(NpcReaction.HumanBigTurn, 2); Run(50000);
        p.Queue(NpcReaction.BotDoomed, 3); Run(70000);
        p.Queue(NpcReaction.BotLethalFound, 4); Run(80000);
        return new Timing { Waits = waits.ToString(), Times = string.Join(",", sent.Select(x => x.At)),
            Later = string.Join(",", sent.Where(x => !x.Start).Select(x => x.Emote)) };
    }

    // NativeMatch level: an NPC conceding to its rival holds the ConcedeTask until Wow and WellPlayed are both sent, bounded.
    private static void ConcessionWaitsForFarewell()
    {
        var profile = NpcCatalog.All[2];   // golden gambler persona: Threaten + mock -> Wow, WellPlayed
        Check(NpcMemoryEmotes.BeatRival(profile.Personality.Emotes, Rival).SequenceEqual(new[] { "Wow", "WellPlayed" }), "fixture farewell");
        foreach (bool stall in new[] { false, true })
        {
            var packets = new List<object>(); var sent = new List<(int Emote, bool Complete)>();
            double now = 0; bool presentationReady = true;
            NativeMatch match = null;
            match = new NativeMatch((id, packet) => packets.Add(packet), profile.DeckId, profile.DeckId, 8427) {
                MatchId = "rival-concede", NativeNpcStartingTaunt = () => false, NpcMemory = Rival,
                PresentationSettings = new NpcPresentationSettings { Emotes = true },
                NativeNpcUserUI = packet => { if (packet.HasEmote) sent.Add((packet.Emote, match.Game.State == SabberStoneCore.Enums.State.COMPLETE)); return true; } };
            match.Start();
            match.NpcClock = () => now; match.NativeNpcIdle = () => true; match.NativePresentationInputReady = () => presentationReady;
            match.SendChoices(packets.OfType<EntityChoices>().Last(p => p.PlayerId == 1).Id, new List<int>());
            match.Game.Process(SabberStoneCore.Tasks.PlayerTasks.EndTurnTask.Any(match.Game.Player1));
            match.Game.Turn = profile.Personality.ConcedeFromTurn;
            foreach (var card in match.Game.Player2.BoardZone.ToArray()) { match.Game.Player2.BoardZone.Remove(card); match.Game.Player2.SetasideZone.Add(card); }
            foreach (var card in match.Game.Player1.BoardZone.ToArray()) { match.Game.Player1.BoardZone.Remove(card); match.Game.Player1.SetasideZone.Add(card); }
            match.Game.Player2.Hero.Damage = match.Game.Player2.Hero.BaseHealth - 4;
            SabberStoneCore.Model.Entities.Entity.FromCard(match.Game.Player1, SabberStoneCore.Model.Cards.FromId("CS2_182"), zone: match.Game.Player1.BoardZone);
            match.MatchId = Enumerable.Range(0, 1000).Select(i => "rival-concede-" + i).First(id =>
                MatchRecorder.PersonalityRoll(profile.Version, id, match.NpcPublicEventIndex, "concede") < profile.Personality.ConcessionWillingness);
            Check(NpcConcession.ProvesPublicBoardLethal(match.Game, 2), "dead fixture not proven");
            now = 100000;
            match.ActNpc(new GoldenDecision(BotAction.From(SabberStoneCore.Tasks.PlayerTasks.EndTurnTask.Any(match.Game.Player2)), 0, 1, 1, "Exhausted", null, null));
            Check(sent.Select(x => x.Emote).SequenceEqual(new[] { (int)EmoteType.WOW }), "Wow sent with the concession decision");
            if (stall) presentationReady = false;   // the follow-up can never become safe: the hold must end on its own
            double decided = now;
            for (int i = 0; i < 100 && match.Game.State != SabberStoneCore.Enums.State.COMPLETE; i++) { now += 100; match.TickNpc(); }
            Check(match.Game.State == SabberStoneCore.Enums.State.COMPLETE && match.OpponentConceded, "concession executed (stall=" + stall + ")");
            if (!stall)
            {
                Check(sent.Select(x => x.Emote).SequenceEqual(new[] { (int)EmoteType.WOW, (int)EmoteType.WELL_PLAYED }) && sent.All(x => !x.Complete),
                    "Wow and WellPlayed both sent before the concession executed: " + string.Join(",", sent));
                Check(now - decided <= NpcPresentation.FollowUpMilliseconds + 500, "hold ends once the follow-up is sent: " + (now - decided));
            }
            else Check(sent.Count == 1 && now - decided >= NativeMatch.ConcessionFarewellHoldMilliseconds
                && now - decided <= NativeMatch.ConcessionFarewellHoldMilliseconds + 200, "bounded hold: " + (now - decided));
            match.StopNpc();
        }
        // No memory: the concession executes on the next idle tick, exactly as before.
        var plainPackets = new List<object>();
        var plain = new NativeMatch((id, packet) => plainPackets.Add(packet), profile.DeckId, profile.DeckId, 8427) {
            MatchId = "plain-concede", NativeNpcStartingTaunt = () => false, PresentationSettings = new NpcPresentationSettings { Emotes = true },
            NativeNpcUserUI = packet => true };
        plain.Start();
        Check(plain.OpponentPlateName == plain.OpponentName, "no memory, plain plate");
        plain.StopNpc();
    }

    // Same seed and NPC encounter with and without memory: identical engine history, NPC actions and final state.
    private static string NoGameplayEffect()
    {
        var baseline = Play(null, out var baseEmotes);
        foreach (var memory in new[] { Rival, Smug })
        {
            var run = Play(memory, out var emotes);
            Check(run.History.Length == baseline.History.Length && run.History.Zip(baseline.History, (a, b) => a.SequenceEqual(b)).All(x => x),
                "power history differs with memory");
            Check(run.Actions.SequenceEqual(baseline.Actions), "NPC action sequence differs with memory:\n" + string.Join("\n", run.Actions.Zip(baseline.Actions, (a, b) => a + " || " + b).Where(l => { var parts = l.Split(" || "); return parts[0] != parts[1]; }).Take(3)));
            Check(baseline.Actions.Length > 0, "NPC took no logged actions");
            Check(run.State == baseline.State, "final state differs: " + run.State + " vs " + baseline.State);
            Check(run.Plate != baseline.Plate && run.EngineName == baseline.EngineName, "only the plate changes: " + run.Plate);
            if (memory == Smug) Check(emotes.FirstOrDefault() == "Thanks", "smug memory was live: " + string.Join(",", emotes));
        }
        return baseline.History.Length + " history packets, " + baseline.Actions.Length + " NPC actions, " + baseline.State.Split(' ')[0]
            + ", emotes " + string.Join("/", baseEmotes);
    }

    private sealed class Outcome
    {
        internal byte[][] History;
        internal string[] Actions;
        internal string State, Plate, EngineName;
    }

    private static Outcome Play(NpcMemory memory, out List<string> emotes)
    {
        var profile = NpcCatalog.All[2];
        var deck = LocalDecks.Get(profile.DeckId);
        var packets = new List<object>(); var log = new List<string>(); var sent = new List<string>();
        double now = 0;
        var match = new NativeMatch((id, packet) => packets.Add(packet), LocalDecks.All[0].Id, deck.Id, 8427) {
            MatchId = "memory-fairness", NativeNpcStartingTaunt = () => false, NpcTurnNodes = 3000, SynchronousNpc = true,
            NpcClock = () => now, NativeNpcIdle = () => true, BotTrace = log.Add,
            PresentationSettings = new NpcPresentationSettings { Emotes = true, Hover = true },
            NativeNpcUserUI = packet => { if (packet.HasEmote) sent.Add(packet.Emote.ToString()); return true; },
            NpcMemory = memory };
        match.Start();
        try
        {
            match.SendChoices(packets.OfType<EntityChoices>().Last(p => p.PlayerId == 1).Id, new List<int>());
            for (int turn = 0; turn < 5 && match.Game.State != SabberStoneCore.Enums.State.COMPLETE; turn++)
            {
                var options = packets.OfType<AllOptions>().Last();
                match.SendOption(options.Id, options.Options.FindIndex(o => o.Type_ == PegasusGame.Option.Type.END_TURN), 0, -1, 0);
                for (int i = 0; match.NpcRunner?.Active == true && i < 20000; i++) { now += 100; match.TickNpc(); }
                Check(match.NpcRunner?.Active != true, "NPC runner stuck");
            }
            for (int i = 0; i < 100; i++) { now += 100; match.TickNpc(); }
            emotes = sent.Select(code => code == ((int)EmoteType.THANKS).ToString() ? "Thanks" : code).ToList();
            var g = match.Game;
            return new Outcome {
                History = packets.OfType<PowerHistory>().Select(p => { using var s = new MemoryStream(); p.Serialize(s); return s.ToArray(); }).ToArray(),
                // Decisions without timing fields (nodes/ms vary with wall clock): the chosen action per NPC step.
                Actions = log.Select(l => System.Text.RegularExpressions.Regex.Match(l, @" turn=(\d+) .* action=(.*?) fallback="))
                    .Where(m => m.Success).Select(m => m.Groups[1].Value + ":" + m.Groups[2].Value).ToArray(),
                State = g.State + " turn=" + g.Turn + " hp=" + g.Player1.Hero.Health + "/" + g.Player2.Hero.Health
                    + " hand=" + string.Join(",", g.Player2.HandZone.Select(c => c.Card.Id)) + " board=" + string.Join(",", g.Player2.BoardZone.Select(c => c.Card.Id))
                    + " p1board=" + string.Join(",", g.Player1.BoardZone.Select(c => c.Card.Id)),
                Plate = match.OpponentPlateName, EngineName = g.Player2.Name };
        }
        finally { match.StopNpc(); }
    }
}
