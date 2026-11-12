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
    // 1-1 after a loss: 0-1 would be a rival now (losses > wins), so the smug fixture must be a real non-rival record.
    private static readonly NpcMemory Smug = new NpcMemory(1, 1, 0, NpcLastMeeting.PlayerLost, false);

    public static void Run()
    {
        Plate();
        var catalog = Personalities();
        ConcessionWaitsForFarewell();
        string fairness = NoGameplayEffect();
        Console.WriteLine("PASS NPC memory: name plate W-L/ties/rival, concession waits for the rival farewell, no gameplay effect (" + fairness + ")");
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
