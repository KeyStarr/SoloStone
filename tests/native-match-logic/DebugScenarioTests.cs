using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using EngineZone = SabberStoneCore.Enums.Zone;

// DebugScenario (plugin/Npc/DebugScenario.cs): parsing, one-shot consumption, exact staging of the NPC's first draw behind
// a hidden transport entity, mana, and the central "a scenario match never settles" gate.
internal static partial class Program
{
    private static void DebugScenarioLanePaths()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int lane = 0; lane <= 3; lane++)
        {
            string root = OfflineGuard.LaneRoot(lane);
            var launch = OfflineGuard.ResolveHeldLaunch(@"LAB\HSNativeUIProbe", "LAB", root + @"\client\Hearthstone.exe",
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

    private static Game CorpseScenarioGame(CardClass human = CardClass.DEATHKNIGHT,
        CardClass npc = CardClass.DEATHKNIGHT, bool enabled = true)
    {
        var game = new Game(new SabberStoneCore.Config.GameConfig {
            RandomSeed = 19, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
            History = true, Logging = false, EnableCorpses = enabled,
            Player1HeroClass = human, Player2HeroClass = npc,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList()
        });
        game.StartGame();
        return game;
    }

    private static void DebugScenarioCorpses()
    {
        var scenario = DebugScenario.Parse("humanCorpses=15\nnpcCorpses=7");
        string canonical = scenario.Canonical();
        Check(canonical == "humanCorpses=15\nnpcCorpses=7"
            && DebugScenario.Parse(canonical).Canonical() == canonical, "Corpses canonical replay");
        Check(DebugScenario.Parse("humanMana=3").HumanCorpses == null, "omitted Corpses stay unset");
        foreach (string key in new[] { "humanCorpses", "npcCorpses" })
        {
            foreach (string bad in new[] { "-1", "+1", "1.5", "ten", "1000", "2147483647", "2147483648", "1,2" })
                RejectsScenario(key + "=" + bad, key + " invalid count");
            RejectsScenario(key + "=1\n" + key + "=2", key + " duplicate");
            Check(DebugScenario.Parse(key + "=999").Canonical() == key + "=999", key + " maximum");
            foreach (var game in new[] { CorpseScenarioGame(CardClass.MAGE, CardClass.MAGE),
                CorpseScenarioGame(enabled: false),
                key == "humanCorpses" ? CorpseScenarioGame(CardClass.MAGE) : CorpseScenarioGame(npc: CardClass.MAGE) })
            {
                var invalid = DebugScenario.Parse(key + "=0\nhumanHand=CS2_182");
                int hand = game.Player1.HandZone.Count;
                Check(invalid.Validate(game)?.Contains(key) == true, key + " requires enabled DK seat, including zero");
                try { invalid.Apply(game); throw new Exception("invalid Corpses scenario applied"); }
                catch (InvalidOperationException) { }
                Check(game.Player1.HandZone.Count == hand
                    && SabberStoneCore.Actions.CorpseRules.Available(game.Player1) == 0
                    && SabberStoneCore.Actions.CorpseRules.Available(game.Player2) == 0, "rejection applies nothing");
            }
        }
        var staged = CorpseScenarioGame();
        Check(scenario.Validate(staged) == null && scenario.Apply(staged) == "humanCorpses=15; npcCorpses=7", "Corpses applied summary");
        Check(SabberStoneCore.Actions.CorpseRules.Available(staged.Player1) == 15
            && SabberStoneCore.Actions.CorpseRules.Available(staged.Player2) == 7, "real Corpses resource seeded per seat");
        var zero = CorpseScenarioGame();
        DebugScenario.Parse("humanCorpses=0\nnpcCorpses=0").Apply(zero);
        Check(SabberStoneCore.Actions.CorpseRules.Available(zero.Player1) == 0
            && SabberStoneCore.Actions.CorpseRules.Available(zero.Player2) == 0, "zero Corpses accepted");
        Console.WriteLine("PASS DebugScenarioCorpses: parsing, replay, per-seat resource, zero, invalid counts and DK gating");
    }

    private static void DebugScenarioCorpsesQuest()
    {
        var game = CorpseScenarioGame();
        var scenario = DebugScenario.Parse("humanCorpses=15\nhumanHand=TLC_433");
        scenario.Apply(game);
        var player = game.Player1;
        player.BaseMana = 10;
        var quest = (Spell)player.HandZone.Single(card => card.Card.Id == "TLC_433");
        Check(game.Process(player.Options().First(task => task.Source?.Id == quest.Id)), "play Reanimate the Terror");
        Check(quest.QuestProgress == 0 && !player.HandZone.Any(card => card.Card.Id == "TLC_433t"), "seeding does not count as spending");
        Check(SabberStoneCore.Actions.CorpseRules.TrySpend(player, 14) && quest.QuestProgress == 14
            && player.SecretZone.ModernQuests.Contains(quest)
            && !player.HandZone.Any(card => card.Card.Id == "TLC_433t"), "quest waits for fifteenth Corpse");
        Check(SabberStoneCore.Actions.CorpseRules.TrySpend(player, 1)
            && SabberStoneCore.Actions.CorpseRules.Available(player) == 0
            && player[GameTag.CORPSES_SPENT_THIS_GAME] == 15, "spend all fifteen staged Corpses");
        Check(quest.Zone == player.GraveyardZone && !player.SecretZone.ModernQuests.Contains(quest)
            && player.HandZone.Count(card => card.Card.Id == "TLC_433t") == 1, "quest completes and Tyrax enters hand");
        Console.WriteLine("PASS DebugScenarioCorpsesQuest: 15 staged Corpses spent completes TLC_433 and puts TLC_433t in hand");
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
