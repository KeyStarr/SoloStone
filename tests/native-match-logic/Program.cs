using System;
using System.Collections.Generic;
using System.Linq;

// The client-free half of the native match checks (docs/TESTING.md): checks that run the engine and the plugin's match logic
// without the client's libraries. No argument runs them all; an argument that names no check fails.
internal static partial class Program
{
    // Fail-open achievement recording must not hide recorder regressions: any evidence invalidation in a match played
    // by these checks (including bot search clones) fails the suite, as in native-match.
    private static readonly System.Collections.Concurrent.ConcurrentQueue<string> evidenceInvalidations = new();

    private static int Main(string[] args)
    {
        typeof(SabberStoneCore.Model.HistoryFacts).GetField("EvidenceInvalidated",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .SetValue(null, (Action<string>)(reason => evidenceInvalidations.Enqueue(reason)));
        ControllerAdmissionInvariant.Install();
        // Process-wide setup that native-match gets from its earlier checks: the world code needs its JSON parser injected.
        NpcWorldJson.Parser = EmbeddedJson.Parse;
        RunSuite(args);
        if (!evidenceInvalidations.IsEmpty)
            throw new InvalidOperationException("TEST: " + evidenceInvalidations.Count + " match(es) invalidated achievement evidence: "
                + string.Join(" | ", evidenceInvalidations.Distinct().Take(5)));
        Console.WriteLine("PASS achievement evidence stayed valid in every match");
        return 0;
    }

    private static void RunSuite(string[] args)
    {
        var slices = new Dictionary<string, Action> {
            ["--npc-runner-only"] = NpcTurnRunnerTests.Run,
            ["--npc-presentation-only"] = NpcPresentationTests.Run,
            ["--rematch-only"] = RematchTests.Run,
            ["--quests-only"] = QuestBotProof.Run,
            ["--showcase-only"] = ShowcasePlayProof.Run,
            ["--npc-pets-only"] = () => { NpcPetSeatTests(); Console.WriteLine("PASS npc pet seats: resolution, forms, world seats"); },
            ["--coin-flip-only"] = () => { CoinFlipControl(); Console.WriteLine("PASS coin flip: launch-bound control"); },
            ["--debug-scenario-only"] = DebugScenarioChecks,
            ["--fair-view-only"] = FairViewChecks,
            ["--match-event-log-only"] = MatchEventLogChecks,
            ["--controller-admission-only"] = ControllerAdmissionControls,
        };
        var unknown = args.Where(arg => !slices.ContainsKey(arg)).ToArray();
        if (unknown.Length != 0) throw new ArgumentException("Unknown argument: " + string.Join(" ", unknown));
        var chosen = args.Length == 0 ? slices.Values.ToArray() : args.Distinct().Select(arg => slices[arg]).ToArray();
        foreach (var run in chosen) run();
        Console.WriteLine("PASS native match logic");
    }

    private static void DebugScenarioChecks()
    {
        DebugScenarioLanePaths();
        AcceptsHeroCardScenario();
        DebugScenarioFile();
        DebugScenarioNeverSettles();
        Console.WriteLine("PASS debug scenario lane paths, hero card scenario, scenario file and never-settles");
    }

    private static void FairViewChecks()
    {
        FairViewHiddenOfferProof.CanonicalPrivacyAssertions();
        B16RetainedMapPrivacy();
        Unpinned_FairIllusionDamage();
        EvTimeFairTimelines();
    }

    private static void MatchEventLogChecks()
    {
        EventBudgetSeparate();
        EventsCannotCloseJournal();
        Console.WriteLine("PASS match event log: separate event budget, events cannot close the journal");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("TEST: " + message);
    }
}
