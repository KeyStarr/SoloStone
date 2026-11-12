using System;
using System.Collections.Generic;
using System.Linq;

// The client-free half of the native match checks (docs/TESTING.md): checks that run the engine and the plugin's match logic
// without the client's libraries. No argument runs them all; an argument that names no check fails.
internal static partial class Program
{
    private static int Main(string[] args)
    {
        ControllerAdmissionInvariant.Install();
        // Process-wide setup that native-match gets from its earlier checks: the world code needs its JSON parser injected.
        NpcWorldJson.Parser = EmbeddedJson.Parse;
        RunSuite(args);
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
            ["--npc-pets-only"] = NpcPetSeatTests,
            ["--coin-flip-only"] = CoinFlipControl,
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
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("TEST: " + message);
    }
}
