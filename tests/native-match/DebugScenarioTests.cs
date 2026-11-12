using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using EngineZone = SabberStoneCore.Enums.Zone;
using NativeOption = PegasusGame.Option;

// DebugScenario (plugin/Npc/DebugScenario.cs): parsing, one-shot consumption, exact staging of the NPC's first draw behind
// a hidden transport entity, mana, and the central "a scenario match never settles" gate.
internal static partial class Program
{
    private static void DebugScenarioTests()
    {
        DebugScenarioLanePaths();
        DebugScenarioParsing();
        DebugScenarioFile();
        DebugScenarioMatch();
        DebugScenarioBoardStats();
        DebugScenarioSummonStats();
        DebugScenarioHandMods();
        DebugScenarioManaRawAndClear();
        DebugScenarioNeverSettles();
        Console.WriteLine("PASS debug scenario: validation, one-shot file, hidden NPC first draw, mana, no settlement");
    }

    private static void DebugScenarioLanePaths()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int lane = 0; lane <= 3; lane++)
        {
            string root = OfflineGuard.LaneRoot(lane);
            var launch = OfflineGuard.ResolveLaunch(@"LAB\HSNativeUIProbe", "LAB", root + @"\client\Hearthstone.exe",
                LabTestPaths.LocalData,
                new[] { "-hs-native-ui-run", RunA, "-hs-native-ui-lane", lane.ToString() }, false);
            string path = launch.Reports + @"\debug-scenario"; // OfflineProbe startup uses launch.Reports.
            Check(path == root + @"\reports\debug-scenario" && paths.Add(path),
                "scenario path follows the validated client lane, including unchanged lane 0: " + lane);
            Check(launch.RunId == RunA, "scenario launch run id stays lane-bound");
        }
    }

    private static void RejectsScenario(string text, string reason, Func<string, bool> known = null)
    {
        try { DebugScenario.Parse(text, known); }
        catch (ArgumentException) { return; }
        throw new Exception("scenario accepted but must be rejected: " + reason);
    }

    // Live 2026-10-30: Jaraxxus (CORE_EX1_323, a HERO card) could not be staged for a freeze repro.
    private static void AcceptsHeroCardScenario()
    {
        var scenario = DebugScenario.Parse("humanHand=CORE_EX1_323\nhumanMana=10\n");
        Check(scenario.HumanHand.SequenceEqual(new[] { "CORE_EX1_323" }), "collectible hero card can be staged in hand");
    }

    private static void DebugScenarioParsing()
    {
        var scenario = DebugScenario.Parse("# comment\r\nnpcDeckTop=EDR_260t, EDR_260t\r\nhumanDeckTop=CS2_182\nnpcMana=10\nhumanMana=3\nnpcHand=CS2_182\n");
        Check(scenario.NpcDeckTop.SequenceEqual(new[] { "EDR_260t", "EDR_260t" }) && scenario.HumanDeckTop.SequenceEqual(new[] { "CS2_182" })
            && scenario.NpcMana == 10 && scenario.HumanMana == 3 && scenario.NpcHand.Count == 1 && scenario.HumanHand.Count == 0,
            "scenario fields parse");
        foreach (string mode in new[] { "human", "npc", "human-godfrey", "npc-godfrey" })
        {
            var burn = DebugScenario.Parse("overdraw=" + mode);
            Check(burn.Overdraw == mode && burn.Canonical() == "overdraw=" + mode, "overdraw scenario roundtrip");
            var (match, _) = AnimationMatch();
            burn.Apply(match.Game);
            var player = mode.StartsWith("npc") ? match.Game.Player2 : match.Game.Player1;
            Check(player.HandZone.Count == 10 && (player.AppliedEnchantments?.Any(e => e.Card.Id == "JAIL_509e") == true) == mode.EndsWith("godfrey"),
                "overdraw scenario fills selected hand and optionally enables actual retention effect");
        }
        var opening = DebugScenario.Parse("humanOpeningHand=CATA_489,CS2_182\nnpcOpeningHand=CATA_134");
        Check(DebugScenario.Parse(opening.Canonical()).HumanOpeningHand.SequenceEqual(new[] { "CATA_489", "CS2_182" }), "opening override roundtrip");
        RejectsScenario("humanOpeningHand=" + string.Join(",", Enumerable.Repeat("CS2_182", 5)), "opening maximum");
        RejectsScenario("npcOpeningHand=NOPE_000", "opening card identity");
        RejectsScenario("humanOpeningHand=GAME_005", "opening override cannot forge Coin");
        var tooMany = new NativeMatch((_, __) => { }, "mage", "priest", 183) {
            StartingPlayer = 1, DebugScenario = DebugScenario.Parse("humanOpeningHand=CATA_489,CATA_489,CATA_489,CATA_489")
        };
        tooMany.Start();
        Check(tooMany.DebugScenario == null && !tooMany.Game.Player1.HandZone.Any(c => c.Card.Id == "CATA_489"),
            "opening override with too few slots is rejected before any transform");
        tooMany.StopNpc();
        RejectsScenario("overdraw=both", "unsupported overdraw mode");
        string canonical = scenario.Canonical();
        Check(DebugScenario.Parse(canonical).Canonical() == canonical && !canonical.Contains("\r"), "canonical text round-trips");
        // Every card the task names exists in the card db (they are the scenarios the coordinator stages).
        Check(DebugScenario.Parse("humanDeckTop=FIR_959,JAIL_500,EDR_031\nnpcDeckTop=EDR_260t").HumanDeckTop.Count == 3,
            "Fyrakk / Slice and Dice / Ohn'ahra ids are valid");
        RejectsScenario("", "empty");
        RejectsScenario("# only a comment", "comment only");
        RejectsScenario("npcDeckTop", "no equals");
        RejectsScenario("npcDeckTop=", "no value");
        RejectsScenario("npcDecktop=EDR_260t", "unknown key");
        RejectsScenario("npcDeckTop=EDR_260t\nnpcDeckTop=EDR_260t", "duplicate key");
        RejectsScenario("npcBoard=CS2_029", "spell cannot be staged on board");
        RejectsScenario("npcBoard=" + string.Join(",", Enumerable.Repeat("EDR_260", 8)), "board limit");
        Check(DebugScenario.Parse("npcBoard=EDR_260\nhumanBoard=CS2_182").Canonical().Contains("npcBoard=EDR_260"), "board scenario round trip");
        // npcBoardStats / humanBoardStats: ATTACK,HEALTH for every minion the board key summons (capture-only).
        var stats = DebugScenario.Parse("humanBoard=DINO_401,EDR_031\nhumanBoardStats=999, 999\nnpcBoard=EDR_260\nnpcBoardStats=0,1");
        Check(stats.HumanBoardStats.Attack == 999 && stats.HumanBoardStats.Health == 999 && stats.NpcBoardStats.Attack == 0
            && stats.NpcBoardStats.Health == 1, "board stats parse (0 attack, 1 health, spaces trimmed)");
        string statsText = stats.Canonical();
        Check(statsText == "npcBoard=EDR_260\nhumanBoard=DINO_401,EDR_031\nnpcBoardStats=0,1\nhumanBoardStats=999,999"
            && DebugScenario.Parse(statsText).Canonical() == statsText, "board stats canonical text round-trips: " + statsText);
        Check(DebugScenario.Parse("humanBoard=CS2_182\nhumanBoardStats=999,999").NpcBoardStats == null, "stats are per side");
        foreach (string bad in new[] { "999", "999,999,1", "1000,5", "5,1000", "5,0", "-1,5", "5,-1", "a,5", "5,", ",5", "5.5,5", "999/999", "+5,5", "5;5", "1e3,5", "0999,5", "5,0999" })
            RejectsScenario("humanBoard=CS2_182\nhumanBoardStats=" + bad, "board stats '" + bad + "'");
        RejectsScenario("humanBoardStats=999,999", "stats without their board key");
        RejectsScenario("humanBoard=CS2_182\nnpcBoardStats=9,9", "stats for the other side's missing board");
        RejectsScenario("humanBoard=CS2_182\nhumanBoardStats=9,9\nhumanBoardStats=9,9", "duplicate stats key");
        try { DebugScenario.Parse("humanBoard=CS2_182\nhumanBoardStats=5,1000"); throw new Exception("health 1000 accepted"); }
        catch (ArgumentException error) { Check(error.Message.Contains("humanBoardStats") && error.Message.Contains("health") && error.Message.Contains("1000"), "stats error names key, field and value: " + error.Message); }
        try { DebugScenario.Parse("humanBoardStats=9,9"); throw new Exception("stats without board accepted"); }
        catch (ArgumentException error) { Check(error.Message.Contains("needs humanBoard"), "stats error names the missing board key: " + error.Message); }
        // npcSummonStats: CARDID,ATK,HEALTH up to 9999 (the board-stats keys stay at 999), final-stage egg tokens stage on npcBoard.
        var rule = DebugScenario.Parse("npcBoard=DINO_410t5,DINO_410t5\nnpcSummonStats=DINO_410t, 9999, 9999");
        Check(rule.NpcSummonStats.CardId == "DINO_410t" && rule.NpcSummonStats.Attack == 9999 && rule.NpcSummonStats.Health == 9999
            && rule.NpcBoard.SequenceEqual(new[] { "DINO_410t5", "DINO_410t5" }), "summon stats and a final-stage egg token parse");
        string ruleText = rule.Canonical();
        Check(ruleText == "npcBoard=DINO_410t5,DINO_410t5\nnpcSummonStats=DINO_410t,9999,9999" && DebugScenario.Parse(ruleText).Canonical() == ruleText,
            "summon stats canonical text round-trips: " + ruleText);
        Check(DebugScenario.Parse("npcSummonStats=DINO_410t,0,1").NpcSummonStats.Attack == 0, "summon stats alone, 0 attack");
        foreach (string bad in new[] { "DINO_410t", "DINO_410t,9999", "DINO_410t,9999,9999,1", "DINO_410t,10000,5", "DINO_410t,5,10000", "DINO_410t,5,0",
            "DINO_410t,05000,5", "DINO_410t,-1,5", "DINO_410t,a,5", "DINO_410t,5.5,5", "DINO_410t;x,5,5", "NOPE_000,5,5", "CS2_029,5,5", "HERO_08,5,5", ",5,5", "DINO_410t/9,5,5" })
            RejectsScenario("npcSummonStats=" + bad, "summon stats '" + bad + "'");
        RejectsScenario("npcSummonStats=DINO_410t,5,5\nnpcSummonStats=DINO_410t,5,5", "duplicate summon stats");
        RejectsScenario("npcSummonStats=DINO_410t,5,5", "client has no definition", id => false);
        RejectsScenario("humanBoard=CS2_182\nhumanBoardStats=1000,5", "board stats keep their 999 limit");
        // humanHandMods: CARDID,COST,ATK,HEALTH,PREMIUM groups for cards humanHand places.
        const string hand = "humanHand=CORE_CS2_189,CORE_EX1_383,CORE_CS2_023,CORE_CS2_029\n";
        const string mods = "humanHandMods=CORE_CS2_189,99,0,1,golden,CORE_EX1_383,0,1,1,signature,CORE_CS2_023,42,-,-,golden,CORE_CS2_029,0,-,-,golden";
        var gag = DebugScenario.Parse(hand + mods);
        Check(gag.HumanHandMods.Count == 4 && gag.HumanHandMods[0].Cost == 99 && gag.HumanHandMods[0].Attack == 0 && gag.HumanHandMods[0].Health == 1
            && gag.HumanHandMods[0].Premium == 1 && gag.HumanHandMods[1].Premium == 3 && gag.HumanHandMods[2].Attack == null
            && gag.HumanHandMods[2].Cost == 42 && gag.HumanHandMods[3].Cost == 0, "hand mods parse (0 cost, 0 attack, - for unchanged)");
        string gagText = gag.Canonical();
        Check(gagText == hand + mods && DebugScenario.Parse(gagText).Canonical() == gagText, "hand mods canonical text round-trips: " + gagText);
        Check(DebugScenario.Parse("humanHand=CORE_CS2_189\nhumanHandMods=CORE_CS2_189,-,9999,9999,diamond").HumanHandMods[0].Premium == 2, "diamond, 9999 stats");
        foreach (string bad in new[] { "CORE_CS2_189,99,0,1", "CORE_CS2_189,99,0,1,golden,CORE_EX1_383", "CORE_CS2_189,100,0,1,golden", "CORE_CS2_189,-1,0,1,golden",
            "CORE_CS2_189,99,10000,1,golden", "CORE_CS2_189,99,0,0,golden", "CORE_CS2_189,99,0,10000,golden", "CORE_CS2_189,99,0,1,platinum", "CORE_CS2_189,99,0,1,Golden",
            "CORE_CS2_189,99,0,1,", "CORE_CS2_189,-,-,-,-", "CORE_CS2_189,ab,0,1,golden", "CORE_CS2_189,099999,0,1,golden", "CORE_CS2_029,0,1,1,golden",
            "CORE_CS2_189,99,0,1,golden,CORE_CS2_189,5,0,1,golden", "CORE_EX1_383,1,-,-,-", "NOPE_000,1,-,-,-" })
            RejectsScenario("humanHand=CORE_CS2_189,CORE_CS2_029\nhumanHandMods=" + bad, "hand mods '" + bad + "'");
        RejectsScenario("humanHandMods=CORE_CS2_189,99,0,1,golden", "hand mods without humanHand");
        RejectsScenario("humanHand=CORE_CS2_189\nhumanHandMods=CORE_CS2_189,99,0,1,golden", "client has no definition", id => id != "CORE_CS2_189");
        // humanHandClear / humanManaRaw / npcManaRaw.
        var raw = DebugScenario.Parse("humanSeat=first\nhumanHandClear=1\nhumanManaRaw=32, 333\nnpcManaRaw=6,777");
        Check(raw.HumanHandClear && raw.HumanManaRaw.Current == 32 && raw.HumanManaRaw.Max == 333 && raw.NpcManaRaw.Max == 777, "raw mana and clear parse");
        string rawText = raw.Canonical();
        Check(rawText == "humanHandClear=1\nhumanManaRaw=32,333\nnpcManaRaw=6,777\nhumanSeat=first" && DebugScenario.Parse(rawText).Canonical() == rawText,
            "raw mana canonical text round-trips: " + rawText);
        Check(!DebugScenario.Parse("humanMana=3").HumanHandClear, "humanHandClear defaults off");
        foreach (string bad in new[] { "humanManaRaw=32", "humanManaRaw=32,333,1", "humanManaRaw=334,333", "humanManaRaw=1,1000", "humanManaRaw=1000,1000", "humanManaRaw=-1,5",
            "humanManaRaw=a,5", "humanManaRaw=0033,333", "humanManaRaw=5.5,9", "humanManaRaw=5/9" })
            RejectsScenario("humanSeat=first\n" + bad, "raw mana '" + bad + "'");
        RejectsScenario("humanManaRaw=32,333", "raw mana without humanSeat");
        RejectsScenario("humanSeat=second\nhumanManaRaw=32,333", "raw mana with the human second");
        RejectsScenario("humanSeat=first\nhumanManaRaw=32,333\nhumanMana=5", "raw mana beside humanMana");
        RejectsScenario("humanSeat=first\nnpcManaRaw=6,777\nnpcMana=5", "raw mana beside npcMana");
        RejectsScenario("humanHandClear=2", "clear takes 0 or 1");
        RejectsScenario("humanSeat=first\nnpcManaRaw=11,777", "NPC raw mana above 10 current sends a negative RESOURCES_USED at its refill");
        Check(DebugScenario.Parse("humanSeat=first\nnpcManaRaw=10,777").NpcManaRaw.Current == 10, "NPC raw mana current 10 is fine");
        RejectsScenario("humanHandClear=1\nhumanOpeningHand=CATA_489", "clear wipes the opening-hand override");
        Check(DebugScenario.Parse("humanHandClear=0\nhumanOpeningHand=CATA_489").HumanOpeningHand.Count == 1, "opening hand without clear is fine");
        RejectsScenario("npcDeckTop=NOPE_000", "unknown card id");
        RejectsScenario("npcDeckTop=EDR_260t,", "empty id");
        RejectsScenario("npcDeckTop=EDR_260t;rm", "bad id characters");
        RejectsScenario("npcDeckTop=HERO_08", "hero card is not playable");
        AcceptsHeroCardScenario();
        RejectsScenario("npcDeckTop=EDR_260t\nnpcMana=0", "mana 0");
        RejectsScenario("npcMana=11", "mana 11");
        RejectsScenario("humanMana=ten", "mana text");
        RejectsScenario("npcDeckTop=" + string.Join(",", Enumerable.Repeat("EDR_260t", DebugScenario.MaxDeckTop + 1)), "too many deck cards");
        RejectsScenario("npcHand=" + string.Join(",", Enumerable.Repeat("CS2_182", DebugScenario.MaxHand + 1)), "too many hand cards");
        RejectsScenario("npcDeckTop=EDR_260t", "client has no definition", id => false);
        RejectsScenario("runId=not a run id\nnpcMana=5", "bad run id");
        RejectsScenario("runId=0b5e3a7e-2f64-4a43-8c3f-4f8b8d2f9a10", "run id only");
        var bound = DebugScenario.Parse("runId=0b5e3a7e-2f64-4a43-8c3f-4f8b8d2f9a10\nnpcMana=5");
        Check(bound.BoundRunId == "0b5e3a7e-2f64-4a43-8c3f-4f8b8d2f9a10" && bound.Canonical() == "npcMana=5",
            "runId binds the file to a launch and stays out of the recorded canonical text");
    }

    private const string RunA = "0b5e3a7e-2f64-4a43-8c3f-4f8b8d2f9a10", RunB = "1c6f4b8f-3a75-4b54-9d40-5a9c9e3ab021";

    private static void DebugScenarioFile()
    {
        string root = Path.Combine(Path.GetTempPath(), "hs-scenario-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "debug-scenario");
            var log = new List<string>();
            Check(DebugScenario.Read(path, log.Add, null, RunA, true) == null && log.Count == 0, "missing file is no scenario");
            File.WriteAllText(path, "runId=" + RunA + "\nnpcDeckTop=EDR_260t\nnpcMana=10\n");
            var scenario = DebugScenario.Read(path, log.Add, null, RunA, true);
            Check(scenario != null && File.Exists(path) && log.Any(line => line.StartsWith("DEBUG_SCENARIO armed")),
                "armed scenario keeps its file until consumed");
            Check(DebugScenario.LastOutcome.Status == "armed" && DebugScenario.LastOutcome.RunId == RunA
                && DebugScenario.LastOutcome.Detail.Contains("npcDeckTop=EDR_260t"), "armed outcome is visible to tools and launch-bound");
            scenario.Consume(log.Add);
            Check(!File.Exists(path) && Directory.GetFiles(root, "debug-scenario.applied-*").Length == 1
                && DebugScenario.Read(path, log.Add, null, RunA, true) == null, "consumed file is renamed away and never applies twice");

            Check(DebugScenario.LastOutcome.Status == "absent" && DebugScenario.LastOutcome.RunId == RunA,
                "missing next-match scenario clears the prior match outcome");
            // Every rejection retires the file whole and applies nothing.
            int rejected = 0;
            void Rejected(string text, string runId, bool control, string why, bool refuses = false, Func<string, bool> known = null)
            {
                File.WriteAllText(path, text);
                log.Clear();
                DebugScenario read = null;
                bool refused = false;
                string profilePath = Path.Combine(root, "profile-" + rejected + ".xml");
                using var profile = new LocalProfile(profilePath);
                // Persist an idle save so refusal must preserve existing bytes, not merely avoid creating a file.
                profile.CancelUnstarted(profile.BeginLaunch("mage", "warrior", 41, _ => { }).Id);
                int stars = profile.Ladder.StarIndex;
                byte[] before = File.ReadAllBytes(profilePath);
                try
                {
                    read = DebugScenario.Read(path, log.Add, known, runId, control);
                    profile.BeginLaunch("mage", "warrior", 42, _ => { });
                }
                catch (InvalidOperationException failure)
                {
                    refused = true;
                    Check(failure.Message.StartsWith("Debug scenario refused: "), "clear launch refusal");
                }
                Check(refused == refuses, "launch refusal: " + why);
                if (refuses)
                    Check(profile.Pending == null && profile.History.Count == 0 && profile.Ladder.StarIndex == stars
                        && File.ReadAllBytes(profilePath).SequenceEqual(before),
                        "refusal leaves save and ladder untouched: " + why);
                else
                    Check(profile.Pending != null && !profile.Pending.DebugScenario, "unbound rejection allows ordinary launch: " + why);
                Check(read == null && !File.Exists(path)
                    && Directory.GetFiles(root, "debug-scenario.rejected-*").Length == ++rejected
                    && log.Single().StartsWith("DEBUG_SCENARIO rejected, nothing applied"), "rejected: " + why + " -> " + string.Join("|", log));
                Check(DebugScenario.LastOutcome.Status == "rejected" && DebugScenario.LastOutcome.RunId == runId
                    && log.Single().Contains(DebugScenario.LastOutcome.Detail), "rejection reason and run id exposed: " + why);
                Thread.Sleep(5);   // distinct retired names
            }
            Rejected("runId=" + RunA + "\nnpcDeckTop=EDR_260t\nnpcMana=99\n", RunA, true, "invalid content", true);
            Rejected("npcMana=99\nrunId=" + RunA, RunA, true, "binding after invalid content", true);
            Rejected("runId=" + RunA + "\nnpcHand=CS2_182", RunA, true, "client has no definition", true, id => false);
            Rejected("runId=" + RunA + "\nrunId=" + RunB + "\nnpcMana=5", RunA, true, "duplicate binding", true);
            Rejected("runId=" + RunB + "\nnpcMana=99", RunA, true, "invalid content from stale launch");
            Rejected("npcMana=99", RunA, true, "invalid content without binding");
            Rejected("runId=" + RunA + "\nnpcMana=99", RunA, false, "invalid content while control off");
            Rejected("npcDeckTop=EDR_260t\n", RunA, true, "no runId line");
            Rejected("runId=" + RunB + "\nnpcDeckTop=EDR_260t\n", RunA, true, "another launch's runId");
            Rejected("runId=" + RunA + "\nnpcDeckTop=EDR_260t\n", RunA, false, "test control is off");
            Rejected("runId=" + RunA + "\nnpcDeckTop=EDR_260t\n", null, true, "this launch has no run id");
            Check(log.Single().Contains("run id") , "no-run-id reason is logged");

            // Boot: a file left by an earlier launch is deleted, never rejected into a match.
            File.WriteAllText(path, "runId=" + RunB + "\nnpcMana=5\n");
            DebugScenario.ClearStale(path);
            Check(!File.Exists(path), "a scenario file does not survive plugin start");
            DebugScenario.ClearStale(path);   // absent: no throw
        }
        finally { Directory.Delete(root, true); }
    }

    private static void DebugScenarioMatch()
    {
        const int seed = 4242;
        var scenario = DebugScenario.Parse("runId=" + RunA + "\nnpcDeckTop=EDR_260t,EDR_260t\nhumanDeckTop=CS2_182,CS2_187\nnpcHand=CS2_182\nnpcMana=10\nhumanMana=3");
        var normalRun = new Capture();
        var normal = new NativeMatch(normalRun.Send, "mage", "warrior", seed);
        normal.Start();
        var run = new Capture();
        var logs = new List<string>();
        var match = new NativeMatch(run.Send, "mage", "warrior", seed) { DebugScenario = scenario, DebugScenarioLog = logs.Add };
        match.Start();
        var game = match.Game;
        Check(match.DebugScenarioApplied == null && game.Player2.DeckZone.All(card => card.Card.Id != "EDR_260t"),
            "nothing is applied before the mulligans finish");
        int npcDeckBefore = game.Player2.DeckZone.Count, humanDeckBefore = game.Player1.DeckZone.Count;
        // Replace the whole hand: the mulligan draws from the deck top, which must still be the match's own cards.
        match.SendChoices(run.Last<EntityChoices>(17).Id, new List<int>());
        normal.SendChoices(normalRun.Last<EntityChoices>(17).Id, new List<int>());
        Check(match.DebugScenarioApplied != null && !match.DebugScenarioApplied.StartsWith("FAILED")
            && logs.Count == 1 && logs[0].StartsWith("DEBUG_SCENARIO applied ") && logs[0].Contains("npcDeckTop=EDR_260t,EDR_260t"),
            "applied once and logged: " + match.DebugScenarioApplied);
        Check(DebugScenario.LastOutcome.Status == "applied" && DebugScenario.LastOutcome.RunId == RunA
            && DebugScenario.LastOutcome.Detail == match.DebugScenarioApplied, "applied outcome matches the actual engine result");
        Check(game.Player1.HandZone.Select(card => card.Card.Id).Take(3).SequenceEqual(normal.Game.Player1.HandZone.Select(card => card.Card.Id).Take(3)),
            "the opening hand and mulligan are the match's normal ones");

        // Human turn 1: first listed card drawn, 3 crystals available.
        Check(game.Turn == 1 && game.Player1.BaseMana == 3 && game.Player1.RemainingMana == 3, "humanMana applied to the first turn");
        bool Created(int id) => !normal.Game.IdEntityDic.ContainsKey(id);
        Check(game.Player1.HandZone.Any(card => card.Card.Id == "CS2_182" && Created(card.Id))
            && game.Player1.DeckZone.TopCard.Card.Id == "CS2_187" && Created(game.Player1.DeckZone.TopCard.Id),
            "human's first draw is the first listed card; the second is next");
        Check(game.Player1.DeckZone.Count == humanDeckBefore + 2 - 1, "human deck holds the two new cards minus the turn-1 draw");
        var npcTop = game.Player2.DeckZone.Reverse().Take(2).ToArray();
        Check(npcTop.All(card => card.Card.Id == "EDR_260t" && Created(card.Id)) && game.Player2.DeckZone.Count == npcDeckBefore + 2,
            "the NPC's next two draws are the two new Illusions");
        Check(game.Player2.BaseMana == 9, "NPC mana is staged one below so its first turn begins at 10");
        var npcHandCard = game.Player2.HandZone.Single(card => card.Card.Id == "CS2_182" && Created(card.Id));

        // Transport: the NPC's new deck and hand entities exist for the client but their identity is hidden.
        var created = new HashSet<int>(npcTop.Select(card => card.Id)) { npcHandCard.Id };
        var history = NativeHistory(run);
        foreach (int id in created)
        {
            var entries = history.Where(entry => SnapshotEntity(entry) == id).ToList();
            Check(entries.Any(entry => entry.HasFullEntity) && entries.All(entry => string.IsNullOrEmpty(SnapshotName(entry))),
                "NPC scenario entity " + id + " is created hidden and never revealed before its draw");
        }
        foreach (var card in game.Player1.HandZone.Where(card => card.Card.Id == "CS2_182"))
            Check(history.Any(entry => SnapshotEntity(entry) == card.Id && SnapshotName(entry) == "CS2_182") || normal.Game.IdEntityDic.ContainsKey(card.Id),
                "human scenario card is visible to the human");
        run.SerializeAll();

        // NPC turn 2: its first draw is the first Illusion, at 10 crystals. Illusion's Topdeck summons it and draws again,
        // so the second Illusion follows at once; both stayed hidden until then.
        int cursor = run.Packets.Count;
        int first = npcTop[0].Id, second = npcTop[1].Id;
        SendEndTurn(match, run);
        Check(game.Turn >= 3 && game.Player2.BaseMana == 10, "NPC played its first turn with 10 crystals");
        Check(game.IdEntityDic[first].Zone.Type != EngineZone.DECK && game.IdEntityDic[second].Zone.Type != EngineZone.DECK,
            "the NPC's first draw was the first scenario Illusion (Topdeck chains into the second)");
        var later = History(run, cursor);
        Check(later.Any(entry => SnapshotEntity(entry) == first && SnapshotName(entry) == "EDR_260t"),
            "the first Illusion is revealed to the client when drawn");
        run.SerializeAll();
        Check(game.Player1.PlayState != PlayState.INVALID, "match still consistent");

        // Applied once: a second pass changes nothing.
        string once = match.DebugScenarioApplied;
        var deckCount = game.Player2.DeckZone.Count;
        typeof(NativeMatch).GetMethod("ApplyDebugScenario", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(match, null);
        Check(match.DebugScenarioApplied == once && game.Player2.DeckZone.Count == deckCount && logs.Count == 1, "scenario applies exactly once");
        // Room is validated BEFORE anything is changed or recorded: a full hand/deck makes Apply throw with the game untouched,
        // and a match whose scenario cannot fit drops it whole (no half-staged game, nothing in the playtest header).
        {
            var fitRun = new Capture();
            var fitLogs = new List<string>();
            var fit = new NativeMatch(fitRun.Send, "mage", "warrior", seed) { DebugScenario = scenario, DebugScenarioLog = fitLogs.Add };
            fit.Start();
            var fitGame = fit.Game;
            Check(fit.DebugScenario != null && fitLogs.Count == 0, "a scenario that fits is kept at start");
            while (fitGame.Player2.HandZone.FreeSpace > 1)
                SabberStoneCore.Model.Entities.Entity.FromCard(fitGame.Player2, Cards.FromId("CS2_182"), zone: fitGame.Player2.HandZone);
            string problem = scenario.Validate(fitGame);
            Check(problem != null && problem.Contains("NPC hand"), "validation names the full hand: " + problem);
            int deck2 = fitGame.Player2.DeckZone.Count, deck1 = fitGame.Player1.DeckZone.Count, entities = fitGame.IdEntityDic.Count;
            try { scenario.Apply(fitGame); throw new Exception("apply on a full hand must throw"); }
            catch (InvalidOperationException) { }
            Check(fitGame.Player2.DeckZone.Count == deck2 && fitGame.Player1.DeckZone.Count == deck1 && fitGame.IdEntityDic.Count == entities
                && fitGame.Player2.BaseMana != 9, "a failed apply changed nothing");
            typeof(NativeMatch).GetMethod("ValidateDebugScenario", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(fit, null);
            Check(fit.DebugScenario == null && fitLogs.Single().StartsWith("DEBUG_SCENARIO aborted, nothing applied"), "an unfittable scenario is dropped whole");
            Check(DebugScenario.LastOutcome.Status == "aborted" && DebugScenario.LastOutcome.RunId == RunA
                && DebugScenario.LastOutcome.Detail == problem, "validation abort reason exposed without applying anything");
            fit.SendChoices(fitRun.Last<EntityChoices>(17).Id, new List<int>());
            Check(fit.DebugScenarioApplied == null && fitGame.Player2.DeckZone.All(card => card.Card.Id != "EDR_260t"), "a dropped scenario never applies");
        }

        {
            var failedRun = new Capture();
            var failed = new NativeMatch(failedRun.Send, "mage", "warrior", seed) { DebugScenario = scenario };
            failed.Start();
            while (failed.Game.Player2.HandZone.FreeSpace > 1)
                SabberStoneCore.Model.Entities.Entity.FromCard(failed.Game.Player2, Cards.FromId("CS2_182"), zone: failed.Game.Player2.HandZone);
            // Room changed after start validation: the Apply catch must expose an abort, never a false applied outcome.
            failed.SendChoices(failedRun.Last<EntityChoices>(17).Id, new List<int>());
            Check(failed.DebugScenarioApplied.StartsWith("FAILED") && DebugScenario.LastOutcome.Status == "aborted"
                && DebugScenario.LastOutcome.RunId == RunA && DebugScenario.LastOutcome.Detail.Contains("NPC hand"),
                "apply-time failure exposes its reason and launch, rather than reporting applied");
        }

        // Determinism (Replay re-applies the recorded scenario): same seed and scenario stage the same game.
        var twinRun = new Capture();
        var twin = new NativeMatch(twinRun.Send, "mage", "warrior", seed) { DebugScenario = scenario };
        twin.Start();
        twin.SendChoices(twinRun.Last<EntityChoices>(17).Id, new List<int>());
        var referenceRun = new Capture();
        var reference = new NativeMatch(referenceRun.Send, "mage", "warrior", seed) { DebugScenario = scenario };
        reference.Start();
        reference.SendChoices(referenceRun.Last<EntityChoices>(17).Id, new List<int>());
        Check(EngineIdentity(twin.Game) == EngineIdentity(reference.Game), "scenario staging is deterministic");
    }

    // The announcement hook (docs/agent-workflows/live-test-hooks.md, variant C): the recipe's exact boards and deck top.
    // Stat overrides must be what the native client holds at match start (HEALTH is max health, DAMAGE stays 0), and the two
    // player actions must run through the real native path (Dracorex attack option, End Turn, the NPC's turn) without a stall.
    private const string AnnouncementHook =
        "humanBoard=DINO_401,TIME_064,EDR_031\nhumanBoardStats=999,999\n"
        + "npcBoard=DINO_410,DINO_410,DINO_410,DINO_410,DINO_410,DINO_410,DINO_410\n"
        + "humanDeckTop=CORE_EX1_011,CATA_308,JAIL_307,CATA_308,JAIL_307,CATA_581,CATA_581\nhumanSeat=first";

    private static void DebugScenarioBoardStats()
    {
        AnnouncementHookMatch(null);              // the recipe verbatim: NPC bodies keep their printed 0/3
        AnnouncementHookMatch("npcBoardStats=0,1"); // plus the NPC-side tag path (not part of the recipe)
    }

    private static void AnnouncementHookMatch(string extra)
    {
        var run = new Capture();
        var logs = new List<string>();
        var scenario = DebugScenario.Parse(AnnouncementHook + (extra == null ? "" : "\n" + extra));
        var match = new NativeMatch(run.Send, "mage", "warrior", 4242) { DebugScenario = scenario, DebugScenarioLog = logs.Add, StartingPlayer = scenario.FirstPlayer };
        match.Start();
        match.SendChoices(run.Last<EntityChoices>(17).Id, new List<int>());
        var game = match.Game;
        Check(match.DebugScenarioApplied != null && !match.DebugScenarioApplied.StartsWith("FAILED")
            && match.DebugScenarioApplied.Contains("humanBoard=DINO_401,TIME_064,EDR_031 stats=999,999")
            && match.DebugScenarioApplied.Contains("npcBoard=DINO_410") == true
            && match.DebugScenarioApplied.Contains("stats=0,1") == (extra != null),
            "board stats applied and logged: " + match.DebugScenarioApplied);
        var history = NativeHistory(run);
        foreach (var minion in game.Player1.BoardZone.Concat(game.Player2.BoardZone))
        {
            bool human = minion.Controller == game.Player1;
            var tags = ReplayNativeEntityTags(history, minion.Id);
            int attack = tags.TryGetValue((int)GameTag.ATK, out int a) ? a : -1, health = tags.TryGetValue((int)GameTag.HEALTH, out int h) ? h : -1;
            int damage = tags.TryGetValue((int)GameTag.DAMAGE, out int d) ? d : 0;
            int expectAttack = human ? 999 : extra != null ? 0 : minion.Card.ATK, expectHealth = human ? 999 : extra != null ? 1 : minion.Card.Health;
            Check(attack == expectAttack && health == expectHealth && damage == 0,
                "the native client holds the staged stats at match start for " + minion.Card.Id + " (" + (human ? "human" : "NPC")
                + "): ATK " + attack + " HEALTH " + health + " DAMAGE " + damage);
            Check(tags.TryGetValue((int)GameTag.ZONE, out int zone) && zone == (int)EngineZone.PLAY, minion.Card.Id + " is on the client board");
            Check(((Minion)minion).AttackDamage == attack && ((Minion)minion).BaseHealth == health && ((Minion)minion).Health == health,
                "engine and client agree on " + minion.Card.Id);
        }
        Check(game.Player1.BoardZone.Count == 3 && game.Player2.BoardZone.Count == 7, "hook boards are staged");

        // Action 1: Dracorex attacks the first Egg of Khelos through a real native option.
        var dracorex = game.Player1.BoardZone.First(m => m.Card.Id == "DINO_401");
        var egg = game.Player2.BoardZone.First(m => m.Card.Id == "DINO_410");
        var options = run.Last<AllOptions>(14);
        int index = options.Options.FindIndex(option => option.Type_ == NativeOption.Type.POWER && option.MainOption?.Id == dracorex.Id);
        Check(index >= 0 && options.Options[index].MainOption.Targets.Any(target => target.Id == egg.Id),
            "Dracorex offers an attack on the egg at turn one");
        int cursor = run.Packets.Count;
        match.SendOption(options.Id, index, egg.Id, -1, 0);
        Check(egg.Zone.Type != EngineZone.PLAY && dracorex.Zone.Type == EngineZone.PLAY, "the egg died, Dracorex stands");
        Check(History(run, cursor).Any(entry => entry.HasTagChange && entry.TagChange.Entity == dracorex.Id), "the attack reached the client");
        // Action 2: End Turn. Ohn'ahra (doubled by Deios) plays six cards, then the NPC takes its turn.
        int deck = game.Player1.DeckZone.Count;
        SendEndTurn(match, run);
        // Six cards from Ohn'ahra (Deios doubles the end-of-turn effect) plus the human's turn-two draw after the NPC's turn.
        Check(deck - game.Player1.DeckZone.Count == 7, "Ohn'ahra played six staged cards and the human drew one, got " + (deck - game.Player1.DeckZone.Count));
        Check(game.Turn >= 3 && game.State != State.COMPLETE, "the NPC's turn ran and the human has the move again");
        foreach (var minion in game.Player1.BoardZone) Check(minion.Zone.Type == EngineZone.PLAY, minion.Card.Id + " survived every wave");
        Check(game.Player1.BoardZone.Count == 3, "the three 999/999 bodies survive");
        if (extra == null)
            Check(game.Player2.GraveyardZone.Count(c => c.Card.Id == "DINO_410t") == 7 && game.Player2.BoardZone.All(m => m.Card.Id != "DINO_410t"),
                "all seven hatched Khelos died to the Decimations through the native path");
        run.SerializeAll();
    }

    // Variant D, the reversal gag (docs/agent-workflows/live-test-hooks.md): seven final-stage eggs; Dracorex's attack kills them
    // and every hatched Khelos must reach the native client as 9999/9999 Taunt (a game-local summon handler, no card edit).
    private static void DebugScenarioSummonStats()
    {
        var run = new Capture();
        var scenario = DebugScenario.Parse("humanBoard=DINO_401,TIME_064,EDR_031\nhumanBoardStats=999,999\n"
            + "npcBoard=DINO_410t5,DINO_410t5,DINO_410t5,DINO_410t5,DINO_410t5,DINO_410t5,DINO_410t5\nnpcSummonStats=DINO_410t,9999,9999\nhumanSeat=first");
        var match = new NativeMatch(run.Send, "mage", "warrior", 4242) { DebugScenario = scenario, StartingPlayer = scenario.FirstPlayer };
        match.Start();
        match.SendChoices(run.Last<EntityChoices>(17).Id, new List<int>());
        var game = match.Game;
        Check(match.DebugScenarioApplied != null && !match.DebugScenarioApplied.StartsWith("FAILED")
            && match.DebugScenarioApplied.Contains("npcSummonStats=DINO_410t,9999,9999"), "summon rule applied: " + match.DebugScenarioApplied);
        Check(game.Player2.BoardZone.Count == 7 && game.Player2.BoardZone.All(m => m.Card.Id == "DINO_410t5" && ((Minion)m).AttackDamage == 0),
            "seven final-stage eggs stand, untouched by the rule until they die");
        var dracorex = game.Player1.BoardZone.First(m => m.Card.Id == "DINO_401");
        var egg = game.Player2.BoardZone.First();
        var options = run.Last<AllOptions>(14);
        int index = options.Options.FindIndex(option => option.Type_ == NativeOption.Type.POWER && option.MainOption?.Id == dracorex.Id);
        Check(index >= 0 && options.Options[index].MainOption.Targets.Any(target => target.Id == egg.Id), "Dracorex offers an attack on an egg");
        match.SendOption(options.Id, index, egg.Id, -1, 0);
        var history = NativeHistory(run);
        var khelos = game.Player2.BoardZone.ToList();
        Check(khelos.Count == 7 && khelos.All(m => m.Card.Id == "DINO_410t"), "every egg hatched a Khelos");
        foreach (var k in khelos)
        {
            var tags = ReplayNativeEntityTags(history, k.Id);
            int attack = tags.TryGetValue((int)GameTag.ATK, out int a) ? a : -1, health = tags.TryGetValue((int)GameTag.HEALTH, out int h) ? h : -1;
            int damage = tags.TryGetValue((int)GameTag.DAMAGE, out int d) ? d : 0;
            Check(attack == 9999 && health == 9999 && (damage == 0 || damage == 999) && tags.TryGetValue((int)GameTag.TAUNT, out int taunt) && taunt == 1
                && tags.TryGetValue((int)GameTag.ZONE, out int zone) && zone == (int)EngineZone.PLAY,
                "the native client holds Khelos " + k.Id + " as 9999/9999 Taunt: ATK " + attack + " HEALTH " + health + " DAMAGE " + damage);
            Check(((Minion)k).AttackDamage == 9999 && ((Minion)k).BaseHealth == 9999, "engine agrees on Khelos " + k.Id);
        }
        Check(khelos.Count(k => ((Minion)k).Damage == 999) == 1, "only the first hatch, born before the shockwave, took 999");
        Check(game.Player1.BoardZone.Count == 3 && game.Player1.BoardZone.All(m => m.Zone.Type == EngineZone.PLAY), "all three of ours stand");
        Check(game.State != State.COMPLETE, "no stall, the match goes on");
        run.SerializeAll();
    }

    // The gag hand of the variant D shot: modified cost, stats and premium finishes must be what the native client holds.
    private static void DebugScenarioHandMods()
    {
        var run = new Capture();
        var scenario = DebugScenario.Parse("humanBoard=DINO_401,TIME_064,EDR_031\nhumanBoardStats=999,999\n"
            + "npcBoard=DINO_410t5,DINO_410t5,DINO_410t5,DINO_410t5,DINO_410t5,DINO_410t5,DINO_410t5\nnpcSummonStats=DINO_410t,9999,9999\n"
            + "humanHand=CORE_CS2_189,CORE_EX1_383,CORE_CS2_023,CORE_CS2_029\n"
            + "humanHandMods=CORE_CS2_189,99,0,1,golden,CORE_EX1_383,0,1,1,signature,CORE_CS2_023,42,-,-,golden,CORE_CS2_029,0,-,-,golden\nhumanSeat=first");
        var match = new NativeMatch(run.Send, "mage", "warrior", 4242) { DebugScenario = scenario, StartingPlayer = scenario.FirstPlayer };
        match.Start();
        match.SendChoices(run.Last<EntityChoices>(17).Id, new List<int>());
        var game = match.Game;
        Check(match.DebugScenarioApplied != null && !match.DebugScenarioApplied.StartsWith("FAILED")
            && match.DebugScenarioApplied.Contains("humanHandMods=CORE_CS2_189,99,0,1,golden"), "hand mods applied: " + match.DebugScenarioApplied);
        // The entities the scenario placed (the Mage deck may also deal its own Fireball and Arcane Intellect, which stay untouched).
        var placed = scenario.StagedPremiums.Keys.Select(id => game.IdEntityDic[id]).ToList();
        Check(placed.Count == 4 && placed.All(card => card.Zone.Type == EngineZone.HAND), "four staged cards sit in the human hand");
        var expect = new Dictionary<string, (int Cost, int? Attack, int? Health, int Premium)> {
            ["CORE_CS2_189"] = (99, 0, 1, 1), ["CORE_EX1_383"] = (0, 1, 1, 3), ["CORE_CS2_023"] = (42, null, null, 1), ["CORE_CS2_029"] = (0, null, null, 1) };
        var history = NativeHistory(run);
        foreach (var card in placed)
        {
            var want = expect[card.Card.Id];
            var tags = ReplayNativeEntityTags(history, card.Id);
            int Tag(GameTag tag) => tags.TryGetValue((int)tag, out int value) ? value : -1;
            Check(Tag(GameTag.COST) == want.Cost && ((IPlayable)card).Cost == want.Cost, "the client holds " + card.Card.Id + " at cost " + want.Cost + ": " + Tag(GameTag.COST));
            if (want.Attack.HasValue)
                Check(Tag(GameTag.ATK) == want.Attack && Tag(GameTag.HEALTH) == want.Health, card.Card.Id + " stats " + Tag(GameTag.ATK) + "/" + Tag(GameTag.HEALTH));
            Check(Tag(GameTag.PREMIUM) == want.Premium, card.Card.Id + " premium tag " + Tag(GameTag.PREMIUM));
            Check(tags.GetValueOrDefault(1932) == 0 && tags.GetValueOrDefault(2589) == (want.Premium == 3 ? 1 : 0), card.Card.Id + " quality flags");
            Check(Tag(GameTag.ZONE) == (int)EngineZone.HAND, card.Card.Id + " is in the client hand");
        }
        // A finish the card lacks is resolved down (Diamond -> Signature -> Golden), never stored as asked.
        Check(NativeMatch.ResolvePremium(2, Cards.FromId("CORE_CS2_189")) == 1 && NativeMatch.ResolvePremium(3, Cards.FromId("CORE_CS2_029")) == 1,
            "ResolvePremium falls back to Golden for a card with no Diamond/Signature");
        {
            var fallbackRun = new Capture();
            var fallback = DebugScenario.Parse("humanHand=CORE_CS2_189,CORE_CS2_029,CORE_EX1_383\nhumanHandMods=CORE_CS2_189,-,-,-,diamond,CORE_CS2_029,-,-,-,signature,CORE_EX1_383,-,-,-,diamond");
            var fb = new NativeMatch(fallbackRun.Send, "mage", "warrior", 4242) { DebugScenario = fallback, StartingPlayer = 1 };
            fb.Start();
            fb.SendChoices(fallbackRun.Last<EntityChoices>(17).Id, new List<int>());
            var fbHistory = NativeHistory(fallbackRun);
            foreach (var pair in fallback.StagedPremiums)
            {
                var card = fb.Game.IdEntityDic[pair.Key];
                int shown = ReplayNativeEntityTags(fbHistory, pair.Key).GetValueOrDefault((int)GameTag.PREMIUM, -1);
                Check(shown == NativeMatch.ResolvePremium(pair.Value, card.Card) && (card.Card.Id == "CORE_EX1_383" ? shown == 3 : shown == 1),
                    "requested finish resolves against " + card.Card.Id + ": client shows premium " + shown);
            }
        }
        // Every finish asked for is one the pinned client data has; a missing one falls back (diamond, signature, golden).
        Check(LocalCardPremium.HasSignature("CORE_EX1_383", "violet-hold"), "Tirion has a Signature version");
        Check(!LocalCardPremium.HasDiamond("CORE_CS2_189", "violet-hold") && !LocalCardPremium.HasSignature("CORE_CS2_189", "violet-hold"),
            "Elven Archer has no Diamond or Signature: Golden is its best finish");
        // The cost holds through the turn-one recalculation and the opening draw already done by main-begin; and after a turn passes.
        SendEndTurn(match, run);
        foreach (var card in placed)
            Check(((IPlayable)card).Cost == expect[card.Card.Id].Cost || card.Zone.Type != EngineZone.HAND, card.Card.Id + " keeps its staged cost after a turn");
        run.SerializeAll();
    }

    // Mana counter gag and gag-only hand: what the client holds (RESOURCES / RESOURCES_USED / TEMP_RESOURCES, the hand zone) and
    // that our turn still works with a 32/333 counter.
    private static void DebugScenarioManaRawAndClear()
    {
        var run = new Capture();
        var scenario = DebugScenario.Parse("humanBoard=DINO_401,TIME_064,EDR_031\nhumanBoardStats=999,999\n"
            + "npcBoard=DINO_410t5,DINO_410t5,DINO_410t5,DINO_410t5,DINO_410t5,DINO_410t5,DINO_410t5\nnpcSummonStats=DINO_410t,9999,9999\n"
            + "humanHandClear=1\nhumanHand=CORE_CS2_189,CORE_EX1_383,CORE_CS2_023,CORE_CS2_029\n"
            + "humanHandMods=CORE_CS2_189,99,0,1,golden,CORE_EX1_383,0,1,1,signature,CORE_CS2_023,42,-,-,golden,CORE_CS2_029,0,-,-,golden\n"
            + "humanManaRaw=32,333\nnpcManaRaw=6,777\nhumanSeat=first");
        var logs = new List<string>();
        var match = new NativeMatch(run.Send, "mage", "warrior", 4242) { DebugScenario = scenario, DebugScenarioLog = logs.Add, StartingPlayer = scenario.FirstPlayer };
        match.Start();
        var game = match.Game;
        var dealt = game.Player1.HandZone.Select(card => card.Id).ToList();
        Check(dealt.Count == 3, "the first player is dealt three cards");
        match.SendChoices(run.Last<EntityChoices>(17).Id, new List<int>());
        Check(logs.Any(line => line.Contains("after main-begin humanManaRaw=32,333; npcManaRaw=6,777")), "raw mana logged: " + string.Join(" | ", logs));
        var history = NativeHistory(run);
        // Gag-only hand: the dealt cards are gone, the four gags and the turn-one draw remain.
        foreach (int id in dealt)
            Check(ReplayNativeEntityTags(history, id).GetValueOrDefault((int)GameTag.ZONE, -1) != (int)EngineZone.HAND && !game.Player1.HandZone.Any(card => card.Id == id),
                "dealt card " + id + " left the hand");
        Check(game.Player1.HandZone.Count == 5, "four gag cards plus the turn-one draw, got " + game.Player1.HandZone.Count);
        Check(scenario.StagedPremiums.Keys.All(id => game.Player1.HandZone.Any(card => card.Id == id)), "all four gags are in the hand");
        // The counter: the client holds the raw numbers.
        foreach (var (player, current, max) in new[] { (game.Player1, 32, 333), (game.Player2, 6, 777) })
        {
            var tags = ReplayNativeEntityTags(history, player.Id);
            int Tag(GameTag tag) => tags.GetValueOrDefault((int)tag, 0);
            Check(Tag(GameTag.RESOURCES) == max && Tag(GameTag.RESOURCES_USED) == max - current && Tag(GameTag.TEMP_RESOURCES) == 0,
                "the client holds " + current + "/" + max + " for player " + player.PlayerId + ": RESOURCES " + Tag(GameTag.RESOURCES)
                + " USED " + Tag(GameTag.RESOURCES_USED) + " TEMP " + Tag(GameTag.TEMP_RESOURCES));
            Check(player.RemainingMana == current && player.BaseMana == max, "engine agrees on player " + player.PlayerId);
        }
        // Our turn still works with the gag counter: options exist and Dracorex's attack resolves.
        Check(game.CurrentPlayer == game.Player1 && game.Turn == 1, "turn one is ours");
        var dracorex = game.Player1.BoardZone.First(m => m.Card.Id == "DINO_401");
        var egg = game.Player2.BoardZone.First();
        var options = run.Last<AllOptions>(14);
        int index = options.Options.FindIndex(option => option.Type_ == NativeOption.Type.POWER && option.MainOption?.Id == dracorex.Id);
        Check(index >= 0, "the attack option exists with a 32/333 counter");
        match.SendOption(options.Id, index, egg.Id, -1, 0);
        Check(game.Player1.RemainingMana == 32 && game.Player1.BaseMana == 333 && game.State != State.COMPLETE, "the counter survives the attack");
        var clock = System.Diagnostics.Stopwatch.StartNew();
        SendEndTurn(match, run);   // not needed for the shot (the coordinator concedes), but it must not hang or throw
        Check(clock.ElapsedMilliseconds < 20000 && game.Turn >= 2, "the NPC turn with a 6/777 counter finishes without a stall: " + clock.ElapsedMilliseconds + " ms");
        // Its turn-start refill clamps the NPC's counter to the engine cap; ours is untouched; the client never sees a negative RESOURCES_USED.
        Check(game.Player2.BaseMana == 10 && game.Player2.RemainingMana >= 0 && game.Player2.RemainingMana <= 10, "the NPC counter is clamped to 10: "
            + game.Player2.RemainingMana + "/" + game.Player2.BaseMana);
        Check(game.Player1.BaseMana == 333 && game.Player1.RemainingMana == 32, "our counter stays 32/333: " + game.Player1.RemainingMana + "/" + game.Player1.BaseMana);
        foreach (var packet in NativeHistory(run))
            if (packet.HasTagChange && packet.TagChange.Tag == (int)GameTag.RESOURCES_USED)
                Check(packet.TagChange.Value >= 0, "RESOURCES_USED never goes negative for entity " + packet.TagChange.Entity + ": " + packet.TagChange.Value);
        run.SerializeAll();
    }

    private static void DebugScenarioNeverSettles()
    {
        string root = Path.Combine(Path.GetTempPath(), "hs-scenario-profile-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            using var profile = new LocalProfile(Path.Combine(root, "profile.xml"));
            // Contrast: an ordinary match leaves a settled row, so a leak below would be visible.
            var ordinary = profile.BeginLaunch("mage", "warrior", 11, _ => { });
            profile.MarkStarted(ordinary.Id);
            profile.Settle(ordinary.Id, "Won", 6, false);
            Check(profile.History.Count == 1, "contrast: an ordinary match settles into the history");
            profile.AcknowledgeRewardPresentation(ordinary.Id);

            int gold = profile.StoredGoldBalance; long dust = profile.DustBalance;
            var stars = profile.Ladder.StarIndex;
            var xp = profile.ClassXp.OrderBy(pair => pair.Key).Select(pair => pair.Key + pair.Value).ToArray();
            int history = profile.History.Count;

            // Armed for exactly one Begin.
            profile.ArmDebugScenario();
            var record = profile.BeginLaunch("mage", "warrior", 12, _ => { });
            Check(record.DebugScenario && profile.Pending.DebugScenario, "the armed Begin is flagged on the pending record");
            profile.MarkStarted(record.Id);
            var settled = profile.Settle(record.Id, "Won", 9, false);
            Check(profile.Pending == null && profile.History.Count == history && profile.StoredGoldBalance == gold && profile.DustBalance == dust
                && profile.Ladder.StarIndex == stars && profile.PendingRewardPresentation == null && !profile.HasPendingRewardPresentation
                && profile.ClassXp.OrderBy(pair => pair.Key).Select(pair => pair.Key + pair.Value).SequenceEqual(xp)
                && settled.DebugScenario && settled.Gold == 0 && settled.Ladder == null && settled.Packs.Count == 0,
                "a scenario match drops its pending record and changes no gold, dust, ladder, XP or history");

            var next = profile.BeginLaunch("mage", "warrior", 13, _ => { });
            Check(!next.DebugScenario, "the next match is an ordinary match again");
            profile.MarkStarted(next.Id);
            profile.AcknowledgeRewardPresentation(ordinary.Id);   // already acknowledged: no-op, not a throw
        }
        finally { Directory.Delete(root, true); }

        // A failed debug settle (save fault) leaves the scenario pending; dropping it (what LocalLadder does next) unblocks starts.
        root = Path.Combine(Path.GetTempPath(), "hs-scenario-fault-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            using var profile = new LocalProfile(Path.Combine(root, "profile.xml"));
            profile.ArmDebugScenario();
            var record = profile.BeginLaunch("mage", "warrior", 15, _ => { });
            profile.MarkStarted(record.Id);
            LocalProfile.CommitFaultHook = phase => { if (phase == "replace") throw new IOException("injected-save"); };
            try { profile.Settle(record.Id, "Won", 7, false); throw new Exception("settle fault missing"); }
            catch (IOException) { }
            finally { LocalProfile.CommitFaultHook = null; }
            Check(profile.Pending?.DebugScenario == true, "a failed debug settle leaves the pending record");
            profile.RecoverInterrupted();
            Check(profile.Pending == null && profile.History.Count == 0, "dropping the scenario pending books nothing");
            Check(profile.BeginLaunch("mage", "warrior", 16, _ => { }) != null, "a new match can start after the failed settle");
        }
        finally { Directory.Delete(root, true); }

        // An interrupted scenario (quit or crash) is dropped on the next boot, never booked as an Abandoned ladder loss.
        root = Path.Combine(Path.GetTempPath(), "hs-scenario-recover-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "profile.xml");
            int stars;
            using (var profile = new LocalProfile(path))
            {
                stars = profile.Ladder.StarIndex;
                profile.ArmDebugScenario();
                var record = profile.BeginLaunch("mage", "warrior", 14, _ => { });
                profile.MarkStarted(record.Id);
                Check(profile.Pending.DebugScenario, "pending scenario persists across a restart");
            }
            using (var profile = new LocalProfile(path))
            {
                Check(profile.Pending?.DebugScenario == true, "reloaded pending keeps its scenario flag");
                profile.RecoverInterrupted();
                Check(profile.Pending == null && profile.History.Count == 0 && profile.Ladder.StarIndex == stars,
                    "recovery of a scenario match books no Abandoned loss");
            }
        }
        finally { Directory.Delete(root, true); }
    }
}

// The one place the tests spell the account's data folder that OfflineGuard.ResolveLaunch validates (the guard holds it privately).
internal static class LabTestPaths
{
    internal const string LocalData = @"C:\Us" + @"ers\HSNative" + @"UIProbe\AppData\Local";
}
