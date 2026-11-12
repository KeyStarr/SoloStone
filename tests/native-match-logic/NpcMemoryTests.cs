using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Rivals-lite (backlog #9): the NPC's memory of the player changes only the native name plate and the emote choice.
internal static class NpcMemoryTests
{
    private static void Check(bool value, string message) { if (!value) throw new Exception("NpcMemory: " + message); }
    private static string Data(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "npc", name));

    public static void Run()
    {
        var catalog = Personalities();
        EmoteTable(catalog);
        PresentationGating(catalog);
        Console.WriteLine("PASS NPC memory: MatchStart and beat-rival emote table for all "
            + catalog.Archetypes.Count + " archetypes, squelch/settings/budget/gap gating, timing identical with memory");
    }

    private static readonly NpcMemory Rival = new NpcMemory(0, 2, 0, NpcLastMeeting.PlayerLost, true);
    private static readonly NpcMemory Respect = new NpcMemory(3, 3, 0, NpcLastMeeting.PlayerWon, false);
    private static readonly NpcMemory Grudge = new NpcMemory(1, 0, 0, NpcLastMeeting.PlayerWon, false);
    // 1-1 after a loss: 0-1 would be a rival now (losses > wins), so the smug fixture must be a real non-rival record.
    private static readonly NpcMemory Smug = new NpcMemory(1, 1, 0, NpcLastMeeting.PlayerLost, false);
    private static readonly NpcMemory Even = new NpcMemory(1, 0, 1, NpcLastMeeting.Tied, false);

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
}
