using System;
using System.IO;

// Lead-owned runner. Each work package owns exactly one suite file and only edits that file.
internal static class Program
{
    internal static string Data(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "npc", name));

    internal static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + message);
    }

    private static int Main(string[] args)
    {
        NpcWorldJson.Parser = text => MiniJSON.Json.Deserialize(text);
        try
        {
            if (args.Length == 1 && args[0] == "--world")
            {
                DeckLibrarySuite.Run(); SkillSuite.Run(); QuestSuite.Run(); MistakeTargetSuite.Run();
                PopulationSuite.Run(); MatchmakerSuite.Run(); PetSuite.Run(); CasualSuite.Run(); CastSuite.Run();
                Console.WriteLine("npc-world deck and world suites passed");
                return 0;
            }
            if (args.Length >= 1 && args[0] == "--cast-report")
            {
                CastSuite.Report(args.Length > 1 ? int.Parse(args[1]) : 20, args.Length > 2 ? int.Parse(args[2]) : 200);
                return 0;
            }
            if (args.Length == 1 && args[0] == "--replacement-lethal") { ReplacementLethalSuite.Run(); return 0; }
            if (args.Length == 1 && args[0] == "--deck-library") { DeckLibrarySuite.Run(); return 0; }
            if (args.Length == 1 && args[0] == "--matchmaker") { MatchmakerSuite.Run(); return 0; }
            if (args.Length == 1 && args[0] == "--magmaw-capped-pass") { CappedPassSuite.RunMagmaw(); return 0; }
            if (args.Length >= 2 && args[0] == "--magmaw-renew") return CappedPassSuite.Renew(args);
            if (args.Length == 1 && args[0] == "--capped-pass") { CappedPassSuite.Run(); return 0; }
            if (args.Length == 1 && args[0] == "--plan-reuse") { PlanReuseMistakeSuite.Run(); return 0; }
            if (args.Length == 1 && args[0] == "--passivity-r3") { RoundThreeSuite.Run(); return 0; }
            if (args.Length == 1 && args[0] == "--passivity-hold") { RoundTwoSuite.Holds(); return 0; }
            if (args.Length == 1 && args[0] == "--passivity-r2") { PassivitySuite.RoundTwo(); return 0; }
            if (args.Length == 1 && args[0] == "--passivity-distribution") { PassivitySuite.Distribution(); return 0; }
            if (args.Length == 1 && args[0] == "--passivity") { PassivitySuite.Run(); return 0; }
            ReplacementLethalSuite.Run();
            CappedPassSuite.Run();
            PlanReuseMistakeSuite.Run();
            PassivitySuite.Run();
            DeckLibrarySuite.Run();   // WP1
            SkillSuite.Run();         // WP2
            QuestSuite.Run();
            MistakeTargetSuite.Run();
            PopulationSuite.Run();    // WP3
            MatchmakerSuite.Run();    // WP4
            PetSuite.Run();           // NPC pets (PROPOSED)
            CasualSuite.Run();        // #37 Standard Casual
            CastSuite.Run();          // #66 named cast
            Console.WriteLine("npc-world tests passed");
            return 0;
        }
        catch (Exception failure) { Console.Error.WriteLine(failure); return 1; }
    }
}
