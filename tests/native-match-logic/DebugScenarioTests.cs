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
    // humanHeroPower=CARDID: parse, canonical, rejection and the applied hero power (ReplaceHeroPower, the heroPowerUpgrade path).
    private static void DebugScenarioHumanHeroPower()
    {
        var staged = DebugScenario.Parse("humanHeroPower=EDR_851p\nhumanMana=5");
        Check(staged.HumanHeroPower == "EDR_851p", "humanHeroPower parses");
        string text = staged.Canonical();
        Check(text == "humanHeroPower=EDR_851p\nhumanMana=5" && DebugScenario.Parse(text).Canonical() == text, "humanHeroPower round-trips: " + text);
        Check(!DebugScenario.Parse("humanMana=5").Canonical().Contains("humanHeroPower"), "an absent key stays absent");
        RejectsScenario("humanHeroPower=CS2_182", "a minion is not a hero power");
        RejectsScenario("humanHeroPower=HERO_08", "a base hero is not a hero power");
        RejectsScenario("humanHeroPower=NO_SUCH_CARD", "unknown card id");
        RejectsScenario("humanHeroPower=TB_BaconShop_HP_011", "a Battlegrounds hero power");
        RejectsScenario("humanHeroPower=TBA01_5", "a Tavern Brawl hero power");
        RejectsScenario("humanHeroPower=EDR_851p,CS2_034", "a list is not an id");
        RejectsScenario("humanHeroPower=EDR_851p\nhumanHeroPower=EDR_851p", "duplicate key");
        RejectsScenario("humanHeroPower=EDR_851p", "no native client definition", id => id != "EDR_851p");
        try { DebugScenario.Parse("humanHeroPower=CS2_182"); throw new Exception("minion accepted"); }
        catch (ArgumentException error) { Check(error.Message.Contains("humanHeroPower") && error.Message.Contains("hero power"), "error names the key: " + error.Message); }
        var game = new SabberStoneCore.Model.Game(new SabberStoneCore.Config.GameConfig { RandomSeed = 17, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
            History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        game.StartGame();
        int before = game.Player1.Hero.HeroPower.Id, enemy = game.Player2.Hero.HeroPower.Id;
        string applied = staged.Apply(game);
        Check(game.Player1.Hero.HeroPower.Card.Id == "EDR_851p" && game.Player1.Hero.HeroPower.Id != before,
            "the human hero power is the staged card: " + game.Player1.Hero.HeroPower.Card.Id);
        Check(game.Player2.Hero.HeroPower.Id == enemy, "the NPC hero power is untouched");
        Check(game.Player1.SetasideZone.Any(e => e.Id == before), "the old hero power left for SETASIDE");
        Check(applied.Contains("humanHeroPower=EDR_851p"), "outcome detail names it: " + applied);
    }

    private static void DebugScenarioWeaponAndGraveyard()
    {
        void Reject(string text, string expected, Func<string, bool> known = null)
        {
            try { DebugScenario.Parse(text, known); }
            catch (ArgumentException error) { Check(error.Message == expected, "scenario rejection: " + error.Message); return; }
            throw new Exception("scenario accepted: " + text);
        }
        const string weapon = "humanWeapon=FP1_021";
        const string grave = "humanGraveyard=CS2_120";
        string canonical = weapon + "\nhumanWeaponDurability=1\n" + grave;
        var scenario = DebugScenario.Parse(canonical);
        Check(scenario.HumanWeapon == "FP1_021" && scenario.HumanWeaponDurability == 1
            && scenario.HumanGraveyard.SequenceEqual(new[] { "CS2_120" }), "weapon and graveyard parse");
        Check(scenario.Canonical() == canonical && DebugScenario.Parse(scenario.Canonical()).Canonical() == canonical, "new keys round-trip");
        var absent = DebugScenario.Parse("humanMana=5");
        Check(absent.HumanWeapon == null && absent.HumanWeaponDurability == 0 && absent.HumanGraveyard.Count == 0
            && absent.Canonical() == "humanMana=5", "absent setup keys stay absent");
        foreach (string key in new[] { "humanWeapon", "humanWeaponDurability", "humanGraveyard" })
            Reject(canonical + "\n" + key + "=1", "Duplicate key: " + key);
        foreach (string value in new[] { "0", "10", "01", "-1", "+1", "1.0", "1e0", "1 0", "999999999999" })
            Reject(weapon + "\nhumanWeaponDurability=" + value, "humanWeaponDurability: out of range");
        foreach (int value in new[] { 1, 9 })
            Check(DebugScenario.Parse(weapon + "\nhumanWeaponDurability=" + value).HumanWeaponDurability == value, "durability boundary " + value);
        Reject("humanWeaponDurability=1", "humanWeaponDurability needs humanWeapon.");
        Reject("humanWeapon=CS2_120", "humanWeapon: CS2_120 is a MINION, not a weapon.");
        Reject("humanWeapon=NO_SUCH_CARD", "humanWeapon: unknown card id NO_SUCH_CARD");
        Reject("humanWeapon=FP1_021!", "humanWeapon has an invalid card id: 'FP1_021!'");
        Reject("humanWeapon=FP1_021,AT_050", "humanWeapon takes at most 1 cards.");
        Reject("humanWeapon=CS2_082", "humanWeapon: CS2_082 has no native client definition.", _ => false);
        // The public card file has no Battlegrounds weapons; restore this test-only set tag before any game runs.
        var battlegroundsWeapon = Cards.FromId("FP1_021");
        int originalSet = battlegroundsWeapon.Tags[GameTag.CARD_SET];
        try
        {
            battlegroundsWeapon.Tags[GameTag.CARD_SET] = 1453;
            Reject(weapon, "humanWeapon: FP1_021 is a Battlegrounds weapon (set 1453), not a constructed one.");
        }
        finally { battlegroundsWeapon.Tags[GameTag.CARD_SET] = originalSet; }
        Reject("humanGraveyard=CS2_029", "humanGraveyard: CS2_029 is not a minion.");
        Reject("humanGraveyard=FP1_021", "humanGraveyard: FP1_021 is not a minion.");
        Reject("humanGraveyard=NO_SUCH_CARD", "humanGraveyard: unknown card id NO_SUCH_CARD");
        Reject(grave + ",", "humanGraveyard has an invalid card id: ''");
        Reject(grave, "humanGraveyard: CS2_120 has no native client definition.", _ => false);
        Check(DebugScenario.Parse("humanGraveyard=" + string.Join(",", Enumerable.Repeat("CS2_120", 7))).HumanGraveyard.Count == 7,
            "seven deaths, including repeated card ids, accepted");
        Reject("humanGraveyard=" + string.Join(",", Enumerable.Repeat("CS2_120", 8)), "humanGraveyard takes at most 7 cards.");

        Game PlainGame(bool corpses = false)
        {
            var game = new Game(new SabberStoneCore.Config.GameConfig { RandomSeed = 17, StartPlayer = 1, Shuffle = false,
                SkipMulligan = true, History = true, Logging = false, EnableCorpses = corpses,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            game.StartGame();
            return game;
        }
        foreach (string id in new[] { "FP1_021", "GVG_036", "AT_050" })
        {
            var game = PlainGame();
            string detail = DebugScenario.Parse("humanWeapon=" + id + "\nhumanWeaponDurability=1\nhumanBoard=GVG_002\nnpcBoard=CS2_120").Apply(game);
            var equipped = game.Player1.Hero.Weapon;
            Check(equipped.Card.Id == id && equipped.Controller == game.Player1 && equipped[GameTag.ZONE] == (int)EngineZone.PLAY
                && equipped.Durability == 1 && equipped.Damage == equipped[GameTag.DURABILITY] - 1,
                "equipped weapon and DAMAGE durability: " + id);
            Check(game.Player2.Hero.Weapon == null && detail.Contains("humanWeapon=" + id), "human-only weapon setup reported");
            int health = game.Player1.BoardZone[0].Health, attack = game.Player1.BoardZone[0].AttackDamage;
            equipped.Damage++;
            game.DeathProcessingAndAuraUpdate();
            Check(game.Player1.Hero.Weapon == null && game.Player1.GraveyardZone.Contains(equipped), "weapon broke: " + id);
            if (id == "FP1_021") Check(game.Player1.BoardZone[0].Health == health - 1 && game.Player2.BoardZone[0].Damage == 1, "Death's Bite deathrattle");
            if (id == "GVG_036") Check(game.Player1.BoardZone[0].Health == health + 2 && game.Player1.BoardZone[0].AttackDamage == attack + 2, "Powermace deathrattle");
            if (id == "AT_050") Check(game.Player1.Hero.HeroPower.Card.Id == "AT_050t", "Charged Hammer deathrattle");
        }
        var defaultGame = PlainGame();
        DebugScenario.Parse(weapon).Apply(defaultGame);
        Check(defaultGame.Player1.Hero.Weapon.Durability == Cards.FromId("FP1_021")[GameTag.DURABILITY]
            && defaultGame.Player1.Hero.Weapon.Damage == 0, "absent durability preserves printed weapon");
        var extended = PlainGame();
        DebugScenario.Parse(weapon + "\nhumanWeaponDurability=9").Apply(extended);
        Check(extended.Player1.Hero.Weapon.Durability == 9
            && extended.Player1.Hero.Weapon.Damage == 0, "durability above printed max still leaves exactly nine");
        var triggerGame = PlainGame();
        DebugScenario.Parse("humanWeapon=EX1_366").Apply(triggerGame);
        var summoned = (Minion)Entity.FromCard(triggerGame.Player1, Cards.FromId("CS2_120"), zone: triggerGame.Player1.BoardZone);
        triggerGame.DeathProcessingAndAuraUpdate();
        Check(summoned.AttackDamage == summoned.Card.ATK + 1 && summoned.Health == summoned.Card.Health + 1
            && triggerGame.Player1.Hero.Weapon.Damage == 1, "equipped Sword of Justice registers summon trigger");
        var resurrection = PlainGame();
        string applied = DebugScenario.Parse(grave + "\nhumanHand=BRM_017").Apply(resurrection);
        var dead = (Minion)resurrection.Player1.GraveyardZone.Single();
        Check(dead.Card.Id == "CS2_120" && dead.ToBeDestroyed && dead.Zone.Type == EngineZone.GRAVEYARD
            && resurrection.Player1.NumFriendlyMinionsThatDiedThisGame == 0 && resurrection.DeadMinions.Count == 0
            && resurrection.Player2.GraveyardZone.Count == 0 && applied.Contains(grave), "friendly death staged without pending deaths");
        resurrection.Player1.BaseMana = 10;
        var resurrect = resurrection.Player1.HandZone.Single(c => c.Card.Id == "BRM_017");
        Check(resurrection.Process(SabberStoneCore.Tasks.PlayerTasks.PlayCardTask.Any(resurrection.Player1, resurrect)), "Resurrect is playable");
        Check(resurrection.Player1.BoardZone.Single().Card.Id == "CS2_120" && resurrection.Player1.GraveyardZone.Contains(dead), "Resurrect summons staged minion");
        var quiet = PlainGame();
        DebugScenario.Parse("humanGraveyard=FP1_002,FP1_002").Apply(quiet);
        quiet.DeathProcessingAndAuraUpdate();
        Check(quiet.Player1.BoardZone.Count == 0 && quiet.Player1.GraveyardZone.Count == 2
            && quiet.Player1.GraveyardZone.All(c => c.ToBeDestroyed) && quiet.Player1.NumFriendlyMinionsThatDiedThisGame == 0
            && quiet.HistoryFacts.Query.Deaths().Count() == 2,
            "staged Haunted Creepers never fire Deathrattles");
        foreach (bool corpses in new[] { false, true })
        {
            var real = PlainGame(corpses);
            var staged = PlainGame(corpses);
            real.Player1[(GameTag)412] = staged.Player1[(GameTag)412] = 6;
            var victim = (Minion)Entity.FromCard(real.Player1, Cards.FromId("CS2_120"), zone: real.Player1.BoardZone);
            victim.ToBeDestroyed = true;
            real.DeathProcessingAndAuraUpdate();
            DebugScenario.Parse(grave).Apply(staged);
            var stagedDead = (Minion)staged.Player1.GraveyardZone.Single();
            var realFact = real.HistoryFacts.Query.Deaths().Single();
            var stagedFact = staged.HistoryFacts.Query.Deaths().Single();
            Check(stagedDead.Zone.Type == victim.Zone.Type && stagedDead.ToBeDestroyed == victim.ToBeDestroyed
                && staged.DeadMinions.Count == 0, "staged death matches real graveyard state");
            Check(real.Player1[(GameTag)412] == 6 && staged.Player1[(GameTag)412] == real.Player1[(GameTag)412],
                "death leaves tag 412 unchanged, including a pre-existing value");
            Check(staged.Player1.NumFriendlyMinionsThatDiedThisTurn == real.Player1.NumFriendlyMinionsThatDiedThisTurn
                && staged.CurrentPlayer.NumMinionsPlayerKilledThisTurn == real.CurrentPlayer.NumMinionsPlayerKilledThisTurn
                && staged.NumMinionsKilledThisTurn == real.NumMinionsKilledThisTurn, "staged death matches real turn counters");
            Check(stagedFact.EntityId == stagedDead.Id && stagedFact.DbfId == realFact.DbfId
                && stagedFact.ControllerId == realFact.ControllerId && stagedFact.CostAtDeath == realFact.CostAtDeath,
                "staged MinionDiedFact matches real death");
            Check(staged.Player1[GameTag.CORPSES] == real.Player1[GameTag.CORPSES]
                && staged.Player1[GameTag.CORPSES] == (corpses ? 1 : 0), "staged corpses match real death with corpses=" + corpses);
            Check(Game.LoadSnapshot(staged.SaveSnapshot()).HistoryFacts.Query.Deaths().Single().EntityId == stagedDead.Id,
                "staged death history survives snapshot validation");
        }
        var fullBoard = PlainGame();
        DebugScenario.Parse("humanBoard=" + string.Join(",", Enumerable.Repeat("CS2_120", 7))
            + "\nhumanWeapon=EX1_366\nhumanGraveyard=FP1_002").Apply(fullBoard);
        fullBoard.DeathProcessingAndAuraUpdate();
        Check(fullBoard.Player1.BoardZone.Count == 7 && fullBoard.Player1.BoardZone.All(m => m.AttackDamage == m.Card.ATK)
            && fullBoard.Player1.Hero.Weapon.Damage == 0 && fullBoard.Player1.GraveyardZone.Count == 1,
            "staged death needs no board slot and fires neither summon triggers nor Deathrattles");
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
