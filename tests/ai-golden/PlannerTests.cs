using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

public static class PlannerTests
{
    internal static string Describe(PlannedTurn r) => string.Join(";", r.Line) + "|" + r.Value.ToString("R", CultureInfo.InvariantCulture) + "|" + r.Nodes + "|" + r.Stop;
    internal static string Trace(DriverTrace t) => t.Kind + "|" + t.FallbackReason + "|" + t.PilotDecision.Action + "|"
        + t.PilotDecision.Value.ToString("R", CultureInfo.InvariantCulture) + "|" + t.PilotDecision.Nodes + "|" + t.PilotDecision.Stop;
    private static Game Attacks()
    {
        var g = Fixture(); Board(g, true, "CS2_182", true); Board(g, true, "CS2_182", true);
        g.Player1.UsedMana = 10; return g;
    }
    public static void TestHoldCandidateIsReported()
    {
        var g = Fixture(); g.Player1.UsedMana = 10;
        var job = TurnPlanner.Job(View(g), NpcCatalog.All[0], DeckPlans.DragonWarrior, 71); Run(job);
        Check(job.Result.Candidates.Any(c => c.Key.Equals(job.Result.Line[0])), "selected END_TURN must have candidate telemetry");
    }
    public static void TestGoldenPilotContract()
    {
        Check(typeof(IFrameSlicedGoldenPilot).IsAssignableFrom(typeof(GoldenPilot)), "frozen pilot interface");
        Check(typeof(GoldenContext).GetConstructor(new[] { typeof(int), typeof(int), typeof(int), typeof(NpcProfile) }) != null,
            "frozen four-argument context constructor remains available");
    }
    public static void TestPlannerSlicesAndWholeDriverHiddenVariants()
    {
        string expected = null;
        var g = Attacks(); Entity.FromCard(g.Player2, Cards.FromId("CS2_182"), zone: g.Player2.HandZone);
        foreach (var variant in new[] { g, Variant(g) }) foreach (int slice in new[] { 1, 7, 1000 }) {
            var job = TurnPlanner.Job(View(variant), NpcCatalog.All[0], DeckPlans.DragonWarrior, 81); Run(job, slice);
            if (expected == null) expected = Describe(job.Result); else Check(expected == Describe(job.Result), "planner hidden/slice mismatch");
            Check(job.Result.Line.Last().Type == PlayerTaskType.END_TURN && job.Result.Line.Count(a => a.Type == PlayerTaskType.MINION_ATTACK) == 2, "whole turn includes both attacks and one end");
        }
        foreach (var profile in NpcCatalog.All) {
            string baseline = null;
            foreach (var variant in new[] { g, Variant(g) }) {
                var pilot = new GoldenPilot(profile); int index = 0;
                var decision = BotDriver.Decide(variant, 1, DeckStyle.Tempo,
                    s => pilot.DecidePilot(s, new GoldenContext(variant.Turn, index, 997 * variant.Turn + index * 31, profile)), ref index, pilot.Options);
                Check(decision.Trace.Kind == "search", "unexpected fallback: " + Trace(decision.Trace));
                if (baseline == null) baseline = Trace(decision.Trace); else Check(baseline == Trace(decision.Trace), "whole driver hidden variant " + profile.Id);
            }
        }
    }
    public static void TestExhaustedTurnKeepsSeparateLethalAllowance()
    {
        var p = NpcCatalog.All[0];
        var tiny = new NpcProfile(p.Id, p.Version, p.DisplayName, p.DeckId, p.HeroSkin, p.CardBack,
            p.Personality, new Budget(1, 1, 1, 1, 1, 1), p.PlanId);
        var pilot = new GoldenPilot(tiny); var state = View(Attacks()); int spent = 0;
        for (int i = 1; i <= 3; i++) spent += pilot.Decide(state, new GoldenContext(state.Facts.Turn, i, 71, tiny)).Nodes;
        Check(spent <= tiny.Budget.TurnNodes + 3 * (tiny.Budget.LethalNodes + 4) && pilot.RemainingNodes == 0, "planning budget and independent lethal allowance exceeded");
    }
    public static void TestDecisionJobsSlicingAndTurnBudget()
    {
        foreach (var profile in NpcCatalog.All) {
            var baseline = View(Attacks());
            var synchronous = new GoldenPilot(profile).Decide(baseline, new GoldenContext(baseline.Facts.Turn, 1, 81, profile));
            string expected = Trace(new DriverTrace("search", synchronous.ToPilotDecision())) + "|" + string.Join(";", synchronous.Candidates);
            foreach (int slice in new[] { 1, 7, 1000 }) {
                var pilot = NpcPilots.Create(profile) as GoldenPilot;
                Check(pilot != null, "catalog must create the real golden pilot");
                var state = View(Attacks());
                var job = ((IFrameSlicedGoldenPilot)pilot).Begin(state, new GoldenContext(state.Facts.Turn, 1, 81, pilot.Profile)); Run(job, slice);
                string actual = Trace(new DriverTrace("search", job.Result.ToPilotDecision())) + "|" + string.Join(";", job.Result.Candidates);
                Check(expected == actual, "whole job slicing " + profile.Id);
                Check(pilot.RemainingNodes >= pilot.Profile.Budget.TurnNodes - job.Result.Nodes && pilot.RemainingNodes < pilot.Profile.Budget.TurnNodes, "planning work charged separately from lethal check");
            }
        }
    }
    public static void TestReuseMatchesReplanAndInvalidatesChanges()
    {
        var state = View(Attacks()); var profile = NpcCatalog.All[0]; var pilot = new GoldenPilot(profile);
        var first = pilot.Decide(state, new GoldenContext(state.Facts.Turn, 1, 81, profile)); state = state.Apply(first.Action);
        var reused = pilot.Decide(state, new GoldenContext(state.Facts.Turn, 2, 112, profile));
        var fresh = new GoldenPilot(profile).Decide(state, new GoldenContext(state.Facts.Turn, 2, 112, profile));
        Check(pilot.PlanReused && reused.Action.Equals(fresh.Action), "deterministic continuation reuses same action");
        var changed = Attacks(); changed.Player1.BoardZone[0].AttackDamage++;
        pilot.Decide(View(changed), new GoldenContext(changed.Turn, 3, 143, profile));
        Check(!pilot.PlanReused, "public change invalidates plan");
    }
    public static void TestPaladinProtectsShield()
    {
        var g = Fixture(CardClass.PALADIN); g.Player1.UsedMana = 10;
        var shield = Board(g, true, "CORE_ICC_038", true); shield.AttackDamage = 3; shield.Health = 3;
        var plain = Board(g, true, "CS2_182", true); plain.AttackDamage = 3; plain.Health = 5;
        var target = Board(g, false, "CS2_231"); target.AttackDamage = 1; target.Health = 1;
        var job = TurnPlanner.Job(View(g), NpcCatalog.All[1], DeckPlans.PurePaladin, 73); Run(job);
        Check(!job.Result.Line.Any(a => a.Source == shield.Id && a.Target == target.Id), "Paladin wastes Divine Shield on trivial ping");
        Check(job.Result.Line.Any(a => a.Source == plain.Id && a.Target == target.Id), "Paladin takes free trade with plain minion");
    }
    public static void TestShamanSequencesFifthBattlecry()
    {
        var g = Fixture(CardClass.SHAMAN);
        new SabberStoneCore.Tasks.SimpleTasks.ReplaceHeroPower(Cards.FromId("JAIL_800hp2")).Process(g, g.Player1, g.Player1.Hero, null);
        for (int i = 0; i < 3; i++) {
            var m = Entity.FromCard(g.Player1, Cards.FromId("CS2_231"), zone: g.Player1.HandZone); m.Cost = 0;
            Check(g.Process(g.Player1.Options().First(t => t.Source?.Id == m.Id)), "setup minion play");
        }
        var filler = Entity.FromCard(g.Player1, Cards.FromId("CS2_231"), zone: g.Player1.HandZone); filler.Cost = 0;
        var payoff = Entity.FromCard(g.Player1, Cards.FromId("JAIL_457"), zone: g.Player1.HandZone); payoff.Cost = 0;
        g.Player1.UsedMana = 10;
        var job = TurnPlanner.Job(View(g), NpcCatalog.All[2], DeckPlans.ZeeShaman, 33); Run(job);
        var plays = job.Result.Line.Where(a => a.Type == PlayerTaskType.PLAY_CARD).ToArray();
        Check(plays.Length >= 2 && plays[0].Source == filler.Id && plays[1].Source == payoff.Id, "fifth minion must double Securitybot, line=" + Describe(job.Result));
        var state = View(g); foreach (var a in plays) state = state.Apply(a);
        Check(state.Facts.Own.Board.Where(c => c.CardId == "CS2_231").All(c => c.Attack >= 3), "doubled buff reaches earlier minions");
    }
    public static void TestRewindThroughDriver()
    {
        foreach (bool good in new[] { true, false }) {
            var g = Fixture(); var announcer = Generic.DrawCard(g.Player1, Cards.FromId("TIME_034"));
            Check(g.Process(g.Player1.Options().First(t => t.Source?.Id == announcer.Id)), "Announcer setup");
            Check(g.Player1.Choice?.ChoiceAction == ChoiceAction.REWIND, "Rewind offer");
            g.Player1.Hero.AddWeapon((Weapon)Entity.FromCard(g.Player1, Cards.FromId(good ? "CORE_RLK_086" : "BE_227")));
            g.Player2.Hero.AddWeapon((Weapon)Entity.FromCard(g.Player2, Cards.FromId(good ? "BE_227" : "CORE_RLK_086")));
            string baseline = null;
            foreach (var variant in new[] { g, Variant(g) }) {
                var pilot = new GoldenPilot(NpcCatalog.All[0]); int index = 0;
                var result = BotDriver.Decide(variant, 1, DeckStyle.Tempo, s => { throw new Exception("Rewind entered search"); }, ref index, pilot.Options);
                Check(result.Trace.Kind == "rewind" && ((ChooseTask)result.Task).Choices.Single() == (good ? variant.Rewind.KeepEntityId : variant.Rewind.RewindEntityId), "plan Rewind policy ignored");
                if (baseline == null) baseline = Trace(result.Trace); else Check(baseline == Trace(result.Trace), "Rewind hidden invariance");
            }
        }
    }
    public static void TestDiscoverPlanTiePreference()
    {
        var eval = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
        Check(eval.DiscoverBonus("CATA_556", "EDR_456") > eval.DiscoverBonus("TLC_600", "EDR_456"), "Dragon Discover tie preference");
        var tie = Fixture(); tie.Player1.UsedMana = 10;
        var preferred = Entity.FromCard(tie.Player1, Cards.FromId("CATA_556"), zone: tie.Player1.SetasideZone);
        var other = Entity.FromCard(tie.Player1, Cards.FromId("TLC_600"), zone: tie.Player1.SetasideZone);
        var source = Entity.FromCard(tie.Player1, Cards.FromId("EDR_456"), zone: tie.Player1.GraveyardZone);
        tie.Player1.Choice = new Choice(tie.Player1) { ChoiceType = ChoiceType.GENERAL, ChoiceAction = ChoiceAction.HAND,
            Choices = new List<int> { other.Id, preferred.Id }, SourceId = source.Id };
        var plan = new DeckPlan("tie", "npc-dragon-warrior", null, null, null, null, null, null, null,
            new[] { new DiscoverPreference("other-source", DiscoverPool.ClassDragons, "test source filter", "TLC_600", "CATA_556") }
                .Concat(DeckPlans.DragonWarrior.DiscoverPreferences), null, new EvaluatorWeights(0,0,0,0,0,0,0,0,0,0,0,0));
        var tiePilot = new GoldenPilot(NpcCatalog.All[0], plan); int tieIndex = 0;
        var selected = BotDriver.Decide(tie, 1, DeckStyle.Tempo,
            s => tiePilot.DecidePilot(View(tie), new GoldenContext(tie.Turn, tieIndex, 71, tiePilot.Profile, tiePilot.PublicInputs)), ref tieIndex, tiePilot.Options);
        Check(selected.Trace.Kind == "search" && ((ChooseTask)selected.Task).Choices.Single() == preferred.Id,
            "real Discover tie must follow plan preference: " + Trace(selected.Trace));
        var g = Fixture(); var card = Generic.DrawCard(g.Player1, Cards.FromId("EDR_856"));
        Check(g.Process(g.Player1.Options().First(t => t.Source?.Id == card.Id)), "Discover setup");
        string expected = null;
        foreach (var variant in new[] { g, Variant(g) }) {
            var pilot = new GoldenPilot(NpcCatalog.All[2]); int index = 0;
            var result = BotDriver.Decide(variant, 1, DeckStyle.Tempo, s => pilot.DecidePilot(s, new GoldenContext(variant.Turn, index, 71, pilot.Profile)), ref index, pilot.Options);
            Check(result.Trace.Kind == "search" && result.Task is ChooseTask, "Discover must resolve a real offer");
            if (expected == null) expected = Trace(result.Trace); else Check(expected == Trace(result.Trace), "Discover hidden invariance");
        }
    }
    public static void TestCrossProcessDeterminism()
    {
        string digest;
        var p = TurnPlanner.Job(View(Attacks()), NpcCatalog.All[0], DeckPlans.DragonWarrior, 71); Run(p, 7);
        var r = ReplyModel.Job(View(Attacks()), NpcCatalog.All[0], 71, new DeckPlanEvaluator(DeckPlans.DragonWarrior)); Run(r, 7);
        digest = Describe(p.Result) + "|" + r.Value.ToString("R", CultureInfo.InvariantCulture) + "|" + Samples(r);
        if (Environment.GetEnvironmentVariable("GOLDEN_DIGEST_CHILD") == "1") { Console.WriteLine("DIGEST " + digest); Environment.Exit(0); }
        for (int i = 0; i < 2; i++) {
            var start = new ProcessStartInfo(Environment.ProcessPath, Assembly.GetExecutingAssembly().Location) {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            start.Environment["GOLDEN_DIGEST_CHILD"] = "1";
            using (var child = Process.Start(start)) {
                string stdout = child.StandardOutput.ReadToEnd(); string stderr = child.StandardError.ReadToEnd(); child.WaitForExit();
                Check(child.ExitCode == 0 && stdout.Split('\n').Contains("DIGEST " + digest), "cross-process mismatch " + stderr);
            }
        }
    }
    public static void TestCalibrationHeadlessGames()
    {
        if (Environment.GetEnvironmentVariable("GOLDEN_CALIBRATE") != "1" || Environment.GetEnvironmentVariable("GOLDEN_DIGEST_CHILD") == "1") return;
        foreach (var profile in NpcCatalog.All) {
            var times = new List<double>(); int decisions = 0, fallbacks = 0, finished = 0, reused = 0; var outOfNodes = new HashSet<int>();
            var opponents = new[] { "showcase-rogue-imbue", "showcase-death-knight-dark-gift", "showcase-demon-hunter-outcast",
                "showcase-druid-quest-tokens", "showcase-hunter-kindred-beasts" };
            for (int seed = 1; seed <= opponents.Length; seed++) {
                var deck = LocalDecks.Get(profile.DeckId); var enemy = LocalDecks.Get(opponents[seed - 1]);
                var g = new Game(new GameConfig { Player1HeroClass = Cards.FromId(enemy.Hero).Class, Player2HeroClass = Cards.FromId(deck.Hero).Class,
                    Player1Deck = enemy.Build(), Player2Deck = deck.Build(), Player2Sideboards = deck.Sideboards,
                    Player1Sideboards = enemy.Sideboards, StartPlayer = 1, Shuffle = true, SkipMulligan = true,
                    History = true, Logging = false, RandomSeed = seed, EnableCorpses = true, FormatType = FormatType.FT_WILD });
                g.StartGame(); var pilot = new GoldenPilot(profile); int index = 0, trackedTurn = -1; double elapsed = 0;
                for (int step = 0; step < 1000 && g.State != State.COMPLETE && g.Turn < 90; step++) {
                    var pending = g.GetPendingDecisions().FirstOrDefault();
                    var owner = pending == null ? g.CurrentPlayer : g.ControllerById(pending.ControllerId);
                    PlayerTask task;
                    if (owner.PlayerId == 2) {
                        if (trackedTurn != g.Turn) { if (trackedTurn != -1) times.Add(elapsed); elapsed = 0; trackedTurn = g.Turn; }
                        var watch = Stopwatch.StartNew();
                        var answer = BotDriver.Decide(g, 2, deck.Style, s => {
                            try { return pilot.DecidePilot(s, new GoldenContext(g.Turn, index, unchecked(997 * g.Turn + index * 31), profile)); }
                            catch (Exception error) { Console.WriteLine("CALIBRATION_SEARCH_ERROR " + error); throw; }
                        }, ref index, pilot.Options);
                        elapsed += watch.Elapsed.TotalMilliseconds; task = answer.Task; decisions++;
                        if (pilot.PlanReused) reused++; if (pilot.RemainingNodes == 0) outOfNodes.Add(seed * 1000 + g.Turn);
                        if (task.PlayerTaskType == PlayerTaskType.END_TURN && pilot.RemainingNodes == 0)
                            Check(LocalBot.Choose(owner.Options()).PlayerTaskType == PlayerTaskType.END_TURN,
                                "exhausted budget forced END_TURN with useful actions remaining");
                        if (answer.Trace.Kind == "fallback") { fallbacks++; Console.WriteLine("CALIBRATION_FALLBACK " + answer.Trace.FallbackReason); }
                    } else task = LocalBot.Choose(owner.Options());
                    if (task is ChooseTask choice) {
                        var d = g.GetPendingDecisions().Single(x => x.ControllerId == owner.Id);
                        Check(g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(d.Id, d.ControllerId, choice.Choices), out var error), error);
                    } else Check(g.Process(task), "headless action rejected");
                }
                if (trackedTurn != -1) times.Add(elapsed); if (g.State == State.COMPLETE) finished++;
            }
            var sorted = times.OrderBy(x => x).ToArray(); double p95 = sorted[(int)Math.Ceiling(sorted.Length * .95) - 1];
            Console.WriteLine("CALIBRATION " + profile.Id + " games=" + finished + "/5 turns=" + times.Count + " decisions=" + decisions + " fallbacks=" + fallbacks
                + " reused=" + reused + " turnNodesOut=" + outOfNodes.Count + " p50ms=" + sorted[(int)Math.Ceiling(sorted.Length * .5) - 1].ToString("F2", CultureInfo.InvariantCulture)
                + " p95ms=" + p95.ToString("F2", CultureInfo.InvariantCulture) + " maxms=" + sorted.Last().ToString("F2", CultureInfo.InvariantCulture));
            Check(finished == 5, "calibration games must finish for " + profile.Id);
            Check(fallbacks == 0, "calibration search fallbacks for " + profile.Id);
            Check(p95 <= 1500, "Mac p95 exceeds 1.5 seconds for " + profile.Id);
        }
    }
}
