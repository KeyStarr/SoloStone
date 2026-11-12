using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Config;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class NpcTurnRunnerTests
{
    private static void Check(bool value, string message)
    { if (!value) throw new Exception("NpcTurnRunner: " + message); }

    public static void Run()
    {
        DeckOnlyLethalStartupMode();
        CoveredSearchClock();
        SearchClockBoundary();
        EliteTurnBudget();
        FastFinishSearchFree();
        FastFinishNativeClockCut();
        PassValidationDeadlines();
        SharedLateSearchDeadline();
        LateLethalPool();
        WorkerWallChargedWhileRunning();
        WrappedSkillPilotWaitsForWorkers();
        EliteWallAwarePlanCap();
        ClockCutUsesContinuations();
        LivePlanNodeCap();
        ReplanReserveCaps();
        DecisionCostLog();
        AdaptiveSlices();
        LethalOutsideTurnBudget();
        ThinkingHoverFrames();
        NativePublicInputs();
        RunnerFailures();
        BusyIdleWaitsPerAction();
        IdleRecoversAfterTimeout();
        IdleBoardHasNoWaitOverhead();
        FreshTurnChecksIdle();
        ActionLimitEndsTheTurn();
        foreach (var profile in NpcCatalog.All)
        {
            var sync = new Rig(profile); sync.Run(true);
            var sliced = new Rig(profile); sliced.Run(false);
            Check(sync.Actions.SequenceEqual(sliced.Actions), "sync/sliced actions differ");
            Check(sliced.Runner.ArtificialDelayMilliseconds <= 4000, "pacing exceeded four seconds");
            Check(!sliced.Runner.ClockCut, "pauses counted as search");
        }
        var slow = new Rig(WithLiveBudget(NpcCatalog.All[0], 5, 0)) { NodeCost = 3000 };
        slow.Run(false);
        Check(slow.Runner.ClockCut && slow.Fallbacks.Contains("clock-cut"), "search ceiling missing");
        var animations = new Rig(NpcCatalog.All[1]) { Idle = false };
        animations.Run(false, 21000);
        Check(!animations.Runner.ClockCut && animations.Log.Any(s => s.Contains("idle-timeout")), "idle time counted as search");
        var cancelled = new Rig(WithLiveBudget(NpcCatalog.All[0], 5, 0));
        cancelled.Now = 2000; cancelled.Runner.Tick(); cancelled.Runner.Cancel(); cancelled.Runner.Tick();
        Check(cancelled.Actions.Count == 0, "cancelled job acted");
        Check(cancelled.Runner.State == NpcTurnState.Cancelled, "cancel state lost");
        HostFailuresAndChoices();
        NativePaths();
        RealGoldenMatches();
        Console.WriteLine("PASS NpcTurnRunner scheduling, native parity, recording, cancellation, timer and legacy isolation");
    }

    // Live budget (round 2): the turn's search stops at 5 s; later decisions keep a short search.
    private static void CoveredSearchClock()
    {
        foreach (bool idle in new[] { false, true })
        {
            var rig = new Rig(WithLiveBudget(NpcCatalog.All[0], 5, 0)) { Idle = idle, NodeCost = 1000 };
            for (int i = 0; rig.Runner.ClockCuts < 1 && i < 200; i++) { rig.Now += 10000; rig.Runner.Tick(); }
            Check(rig.Runner.SearchMilliseconds == NpcTurnRunner.TurnSearchMilliseconds && rig.Runner.ClockCut
                && rig.Fallbacks.SequenceEqual(new[] { "clock-cut" }), "search not cut at five seconds (idle=" + idle + ")");
            Check(idle ? rig.Runner.FreezeSearchMilliseconds == 5000 && rig.Runner.CoveredSearchMilliseconds == 0
                : rig.Runner.CoveredSearchMilliseconds == 5000 && rig.Runner.FreezeSearchMilliseconds == 0,
                "freeze/covered accounting (idle=" + idle + ")");
        }
        var sync = new Rig(WithLiveBudget(NpcCatalog.All[0], 5, 0)) { NodeCost = 1000 };
        sync.Run(true);
        Check(sync.Log.Any(line => line.Contains("freeze=5000.00") && line.Contains("ms=5000.00") && line.Contains("limit=5000")),
            "decision log omitted freeze timing or limit");
        Check(sync.Log.Any(line => line.Contains(" turn-summary ") && line.Contains("clockCut=1") && line.Contains("decisions=")
            && line.Contains("nodes=") && line.Contains("searchMs=") && line.Contains("msPerFrame=")), "turn summary omitted the search budget fields");
        var rig2 = new Rig(WithLiveBudget(NpcCatalog.All[0], 5, 0)) { NodeCost = 1000 };
        rig2.Run(false, 10000);
        rig2.Runner.Start();
        Check(rig2.Runner.SearchMilliseconds == 0 && rig2.Runner.FreezeSearchMilliseconds == 0 && rig2.Runner.ClockCuts == 0
            && rig2.Runner.CoveredSearchMilliseconds == 0 && !rig2.Runner.ClockCut && rig2.Runner.Decisions == 0, "fresh turn retained search clocks");
    }

    // Elite+ (2026-10-25): Budget.TurnSearchSeconds replaces the 5 s turn budget for that profile only, and
    // Budget.TurnWallSeconds stops new search once the turn's unpaused wall time passes the guard.
    private static NpcProfile WithLiveBudget(NpcProfile p, double searchSeconds, double wallSeconds)
    {
        var b = p.Budget;
        return new NpcProfile(p.Id, p.Version, p.DisplayName, p.DeckId, p.HeroSkin, p.CardBack, p.Personality,
            new Budget(b.TurnNodes, b.PlanNodes, b.LethalNodes, b.ReplyK, b.ReplyL, b.ReplyR, b.OwnBeamWidth, b.Dedupe, searchSeconds, wallSeconds),
            p.PlanId, p.HeroSkinPower);
    }
    private static void EliteTurnBudget()
    {
        Check(NpcCatalog.All.All(p => p.Budget.TurnSearchSeconds == 45 && p.Budget.TurnWallSeconds == 55 && !p.Budget.Dedupe),
            "shipped golden profiles must use 45/55 guards");
        var rig = new Rig(WithLiveBudget(NpcCatalog.All[0], 20, 0)) { NodeCost = 1000 };
        for (int i = 0; rig.Runner.ClockCuts < 1 && i < 400; i++) { rig.Now += 10000; rig.Runner.Tick(); }
        Check(rig.Runner.ClockCut && rig.Runner.SearchMilliseconds == 20000, "elite+ turn search budget: " + rig.Runner.SearchMilliseconds);
        var guarded = new Rig(WithLiveBudget(NpcCatalog.All[0], 60, 12)) { NodeCost = 1000 };
        for (int i = 0; guarded.Runner.ClockCuts < 1 && i < 400; i++) { guarded.Now += 10000; guarded.Runner.Tick(); }
        Check(guarded.Runner.ClockCut && guarded.Runner.SearchMilliseconds <= 12000, "elite+ wall guard: " + guarded.Runner.SearchMilliseconds);
    }

    // Fast finish (live incident 2026-11-01): after a clock-cut fallback chose a fast-finish action, later decisions of
    // the turn skip search while the wall stays exhausted. An ordinary fallback keeps searching every decision.
    private static void FastFinishSearchFree()
    {
        var profile = WithLiveBudget(NpcCatalog.All[0], 45, 1);
        var deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList();
        var game = new SabberStoneCore.Model.Game(new GameConfig { Player1HeroClass = CardClass.WARRIOR, Player2HeroClass = CardClass.WARRIOR,
            Player1Deck = deck, Player2Deck = deck.ToList(), StartPlayer = 1, SkipMulligan = true, Shuffle = false });
        game.StartGame(); game.Player1.BaseMana = 10;
        var end = BotAction.From(EndTurnTask.Any(game.Player1));
        var free = BotAction.From(game.Player1.Options().First(t => t.PlayerTaskType != PlayerTaskType.END_TURN));
        foreach (bool finish in new[] { true, false })
        {
            double now = 0; int begins = 0; var reasons = new List<string>(); var acted = new List<string>(); var log = new List<string>();
            var runner = new NpcTurnRunner(profile, "fast-finish", () => now, () => acted.Count < 12 && !acted.Contains(end.ToString()),
                () => new GoldenContext(2, acted.Count + 1, 71, profile), ctx => { begins++; return new StallJob(() => now += 1000); },
                reason => {
                    reasons.Add(reason);
                    var pick = reasons.Count <= 3 ? free : end;
                    var made = new GoldenDecision(pick, 0, 0, 0, "Forced", reason, null, finish && FastFinish.Applies(reason) ? FastFinish.DriverKind : "fallback");
                    if (finish && FastFinish.Applies(reason)) made.SearchInfo.FastFinish = pick.Equals(end) ? FastFinish.None : FastFinish.Attack;
                    return made;
                },
                decision => acted.Add(decision.Action.ToString()), () => true, log.Add);
            runner.Start();
            for (int i = 0; runner.Active && i < 100000; i++) { now += 16; runner.Tick(); }
            Check(!runner.Active && acted.Count == 4 && acted.Last() == end.ToString(), "fast finish turn " + string.Join(",", acted));
            if (finish)
            {
                Check(begins == 1 && reasons.SequenceEqual(new[] { "clock-cut", FastFinish.Reason, FastFinish.Reason, FastFinish.Reason }),
                    "fast finish must be search-free after it fired: begins " + begins + " reasons " + string.Join(",", reasons));
                Check(log.Count(l => l.Contains(" fastFinish=attack")) == 3 && log.Count(l => l.Contains(" fastFinish=none")) == 1,
                    "LOCAL_AI fastFinish field: " + string.Join("|", log.Where(l => l.Contains(" action="))));
            }
            else Check(begins == 4 && reasons.All(r => r == "clock-cut") && log.Where(l => l.Contains(" action=")).All(l => l.Contains(" fastFinish=- ") && l.EndsWith(" replan=-")),
                "ordinary fallback keeps searching: begins " + begins + " reasons " + string.Join(",", reasons));
        }
    }
    private sealed class StallJob : IGoldenDecisionJob
    {
        private readonly Action step;
        internal StallJob(Action step) { this.step = step; }
        public bool IsDone => false;
        public GoldenDecision Result => null;
        public string LethalResult => "none";
        public bool PlanReused => false;
        public void StepNode() => step();
    }

    // Native host, real golden pilot: a clock that blows the 55 s wall at once reproduces the incident's clock-cut fallback
    // (planNodeCap=0, no fallback node). Ready minions must still attack (fastFinish=attack), never END_TURN with them
    // idle. With a clock that does not run out the same position never enters fast finish.
    private static void FastFinishNativeClockCut()
    {
        var previous = NpcPilots.Factory;
        try
        {
            NpcPilots.Factory = null;
            foreach (bool exhausted in new[] { true, false })
            {
                var rig = new NativeRig(NpcCatalog.All[0].DeckId, false, clockStep: exhausted ? 5000 : 0);
                var game = rig.Match.Game; var bot = game.Player2;
                foreach (var id in new[] { "CS2_120", "CS2_182" })
                {
                    var minion = (SabberStoneCore.Model.Entities.Minion)SabberStoneCore.Model.Entities.Entity.FromCard(bot, Cards.FromId(id), zone: bot.BoardZone);
                    minion.IsExhausted = false;
                }
                game.SummonedMinions.Clear();
                int before = game.Player1.Hero.Health + game.Player1.Hero.Armor;
                rig.EndHumanTurn();
                var lines = rig.Log.Where(l => l.Contains(" action=") && l.Contains(" fastFinish=")).ToList();
                if (exhausted)
                    Check(lines.Any(l => l.Contains(" fastFinish=attack")) && game.Player1.Hero.Health + game.Player1.Hero.Armor <= before - 6,
                        "exhausted wall: ready minions must attack before END_TURN: " + string.Join("|", lines));
                else
                    Check(lines.Count > 0 && lines.All(l => l.Contains(" fastFinish=- replan=")), "normal budget entered fast finish: " + string.Join("|", lines));
                rig.Match.StopNpc();
            }
        }
        finally { NpcPilots.Factory = previous; }
    }

    private sealed class ValidationClockJob : IGoldenDecisionJob, INpcPassValidation, INpcSearchContinuation
    {
        private readonly GoldenDecisionJob inner;
        private readonly Action advance;
        internal ValidationClockJob(GoldenDecisionJob inner, Action advance) { this.inner = inner; this.advance = advance; }
        public bool IsDone => inner.IsDone;
        public GoldenDecision Result => inner.Result;
        public string LethalResult => inner.LethalResult;
        public bool PlanReused => inner.PlanReused;
        public bool Validating => inner.Validating;
        public GoldenDecision StopValidation() => inner.StopValidation();
        public GoldenDecision StopSearch(SearchStop stop) => inner.StopSearch(stop);
        public void StepNode() { advance(); inner.StepNode(); }
    }
    private static void PassValidationDeadlines()
    {
        var skills = SkillCatalog.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "npc", "skills-v1.json")));
        Check(skills.Tiers.All(t => t.Budget.TurnSearchSeconds == 45 && t.Budget.TurnWallSeconds == 55), "all shipped tiers use 45/55");
        // Real Golden reserve, controlled per-node clock. Both a budget cut and a node cut must remain sliced.
        foreach (string mode in new[] { "complete", "wall", "nozdormu", "timer-option", "already-expired", "completed-comparison", "node-budget", "search-budget", "search-and-wall" })
        {
            var profile = NpcCatalog.All[0]; var deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList();
            var game = new SabberStoneCore.Model.Game(new GameConfig { Player1HeroClass = CardClass.WARRIOR, Player2HeroClass = CardClass.MAGE,
                Player1Deck = deck, Player2Deck = deck.ToList(), StartPlayer = 1, SkipMulligan = true, Shuffle = false, RandomSeed = 7 });
            game.StartGame();
            foreach (var player in new[] { game.Player1, game.Player2 })
                foreach (var card in player.HandZone.ToArray()) { player.HandZone.Remove(card); player.SetasideZone.Add(card); }
            game.Player1.BaseMana = 2; game.Player1.UsedMana = 0;
            var own = (SabberStoneCore.Model.Entities.Minion)SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, Cards.FromId("CS2_182"), zone: game.Player1.BoardZone);
            own[GameTag.ATK] = 3; own[GameTag.HEALTH] = 3; own.IsExhausted = false;
            var enemy = (SabberStoneCore.Model.Entities.Minion)SabberStoneCore.Model.Entities.Entity.FromCard(game.Player2, Cards.FromId("CS2_182"), zone: game.Player2.BoardZone);
            enemy[GameTag.ATK] = 1; enemy[GameTag.HEALTH] = 1; enemy[GameTag.TAUNT] = 1;
            game.SummonedMinions.Clear();
            var pilot = new GoldenPilot(profile);
            var view = pilot.Options.FairViewFactory(game, 1, 71, DeckStyle.Control);
            pilot.Job(view, new GoldenContext(game.Turn, 1, 71, profile));
            var power = view.Actions.Single(a => a.Type == PlayerTaskType.HERO_POWER);
            var end = view.Actions.Single(a => a.Type == PlayerTaskType.END_TURN);
            pilot.Accept(view, new[] { power, end }, 0, LethalOutcome.Incomplete, true);
            pilot.ChargePlan(int.MaxValue); view = view.Apply(power);
            var job = pilot.Job(view, new GoldenContext(game.Turn, 2, 71, profile));
            // StopSearch starts validation without executing a reserve node on the caller's frame.
            if (mode != "node-budget" && mode != "search-budget" && mode != "search-and-wall")
                Check(job.StopSearch(SearchStop.Clock) == null && job.Validating, "cut must leave validation pending: " + mode);
            if (mode == "completed-comparison") {
                var field = typeof(GoldenDecisionJob).GetField("validation", BindingFlags.Instance | BindingFlags.NonPublic);
                var planner = (TurnPlannerJob)field.GetValue(job);
                while (!planner.HasPassComparison && !job.IsDone) job.StepNode();
                Check(planner.HasPassComparison && !job.IsDone, "completed pair available before publication");
            }
            double now = 0, lastNodeAt = 0; int acted = 0; GoldenDecision selected = null; var log = new List<string>();
            double deadlineAt = mode == "nozdormu" ? 15000 : mode == "timer-option" ? 8000 : double.PositiveInfinity;
            var runner = new NpcTurnRunner(profile, "validation-" + mode, () => now, () => acted == 0,
                () => new GoldenContext(game.Turn, 2, 71, profile),
                ctx => new ValidationClockJob(job, () => { now += (mode == "search-budget" || mode == "search-and-wall") && !job.Validating ? 45000 : 10; lastNodeAt = now; }),
                reason => throw new Exception("validation discarded: " + reason),
                decision => { selected = decision; acted++; }, () => true, log.Add,
                turnNodes: mode == "node-budget" ? 0 : (int?)null,
                turnDeadlineLeft: () => deadlineAt - now);
            runner.SliceMilliseconds = runner.IdleSliceMilliseconds = runner.MaxIdleSliceMilliseconds = 1;
            runner.Start(); runner.Tick(true);
            if (mode == "wall") now = 54900;
            if (mode == "already-expired" || mode == "completed-comparison") now = 55001;
            if (mode == "nozdormu") now = 9900;
            if (mode == "timer-option") now = 2900;
            int frames = 0;
            for (; runner.Active && frames < 20000; frames++) {
                now += 1; double before = now; bool primaryNode = !job.Validating && !job.IsDone; runner.Tick();
                Check((mode == "search-budget" || mode == "search-and-wall") && primaryNode || now - before <= 20, "reserve drained in one frame: " + mode + " ms=" + (now - before));
                if (mode == "search-and-wall" && !primaryNode && job.Validating) now = 55001;
            }
            bool cutoff = mode != "complete" && mode != "node-budget" && mode != "search-budget";
            Check(acted == 1 && selected.SearchInfo.ValidationDeadline == cutoff, "deadline flag: " + mode);
            Check(runner.Decisions == 1 && runner.ClockCuts == (cutoff || mode == "search-budget" ? 1 : 0),
                "clock cuts counted once per decision: " + mode + " decisions=" + runner.Decisions + " cuts=" + runner.ClockCuts);
            bool compared = !cutoff || mode == "completed-comparison";
            Check(selected.SearchInfo.ValidationCompared == compared && selected.SearchInfo.ValidationFallback == !compared,
                "completed comparison/fallback flags: " + mode);
            Check(selected.Action.Type == (compared ? PlayerTaskType.MINION_ATTACK : PlayerTaskType.END_TURN), "safe cutoff action: " + mode);
            Check(selected.SearchInfo.UnscheduledNodes > 0 || mode == "already-expired" || mode == "completed-comparison", "reserve nodes accounted: " + mode);
            if (cutoff && mode != "already-expired" && mode != "completed-comparison")
                Check(lastNodeAt <= Math.Min(55000, deadlineAt - NpcTurnRunner.DeadlineReserveMilliseconds) + 10, "reserve passed hard cutoff: " + mode);
            Check(log.Any(l => l.Contains(" unscheduledNodes=") && l.Contains(" validationDeadline=" + (cutoff ? 1 : 0))
                && l.Contains(" validationFallback=" + (compared ? 0 : 1))), "LOCAL_AI validation telemetry: " + mode);
        }
        Console.WriteLine("PASS real pass validation: sliced reserve, 55 s wall, finite 15/8 s deadlines, safe fallback and completed comparison");
    }

    // Elite+ review (2026-10-25): live search shares the wall with frames (think wall ~1.5-1.8x search), so the plan cap
    // comes from the wall left before the guard at the measured search share. A stepping clock with frame gaps: every
    // planned decision must finish by nodes before the guard, never clock-cut by it.
    private sealed class WallRig
    {
        internal double Now; internal int Acted;
        // Planner nodes a search wants (400k: all it is allowed).
        internal int Spend = 400000;
        internal readonly List<int> Caps = new List<int>(); internal readonly List<string> Stops = new List<string>(), Log = new List<string>();
        internal readonly List<double> ActedAt = new List<double>();
        internal readonly NpcTurnRunner Runner;
        internal WallRig(NpcProfile profile, int actions, double nodeCost, int before, double deadlineAt = double.PositiveInfinity)
        {
            Func<double> deadlineLeft = double.IsInfinity(deadlineAt) ? null : (Func<double>)(() => deadlineAt - Now);
            var game = new SabberStoneCore.Model.Game(new GameConfig { Player1HeroClass = CardClass.WARRIOR, Player2HeroClass = CardClass.WARRIOR });
            var action = BotAction.From(EndTurnTask.Any(game.Player2));
            Runner = new NpcTurnRunner(profile, "wall-cap", () => Now, () => Acted < actions,
                () => new GoldenContext(2, Acted, 71, profile),
                ctx => new CapJob(action, () => Now += nodeCost, Caps, Spend, before),
                reason => { Stops.Add("fallback:" + reason); return new GoldenDecision(action, 0, 0, 0, "Clock", reason, null, "fallback"); },
                decision => { Stops.Add(decision.Stop); ActedAt.Add(Now); Acted++; }, () => true, Log.Add, turnNodes: 2000000,
                turnDeadlineLeft: deadlineLeft);
        }
        // Frames: 30 ms of search, then 30 ms of other work, a 150 ms hitch every 10th frame (search share ~0.42).
        internal void Run(int frames = 1000000)
        {
            for (int i = 0; Runner.Active && i < frames; i++) { Now += i % 10 == 9 ? 150 : 30; Runner.Tick(); }
        }
    }
    // replan-reserve: the first search keeps its whole cap; on a clock-limited turn each replan leaves the next a share. A tier
    // whose plan budget fits the clock, a hard deadline (Nozdormu) and the flag OFF keep the old chain.
    private static void ReplanReserveCaps()
    {
        NpcProfile Tier(int plan, bool on)
        {
            var p = WithLiveBudget(NpcCatalog.All[0], 45, 55); var b = p.Budget;
            return new NpcProfile(p.Id, p.Version, p.DisplayName, p.DeckId, p.HeroSkin, p.CardBack, p.Personality,
                new Budget(plan * 4, plan, b.LethalNodes, b.ReplyK, b.ReplyL, b.ReplyR, b.OwnBeamWidth, b.Dedupe, 45, 55, b.Brain), p.PlanId, p.HeroSkinPower,
                new AiFixes(on ? new string[0] : new[] { "replan-reserve" }));
        }
        // Each search spends 3,000 nodes: enough left for a chain, and the same first spend in every arm.
        List<int> Chain(NpcProfile profile, double deadlineAt = double.PositiveInfinity)
        {
            var rig = new WallRig(profile, 5, .5, 2000, deadlineAt) { Spend = 3000 }; rig.Runner.Start(); rig.Run();
            Check(rig.Acted == 5, "reserve rig finished");
            return rig.Caps;
        }
        string Show(List<int> caps) => string.Join(",", caps);
        var on = Chain(Tier(70000, true)); var off = Chain(Tier(70000, false));
        Check(on[0] == off[0], "the first search keeps its whole cap: " + Show(on) + " vs " + Show(off));
        Check(on[1] < off[1] * .7 && on[1] > off[1] * .5, "a replan leaves a share: " + Show(on) + " vs " + Show(off));
        Check(on.Skip(1).All(c => c >= NpcTurnRunner.ViableReplanNodes || c >= NpcTurnRunner.MinLivePlanNodes && c < 2 * NpcTurnRunner.ViableReplanNodes), "chain stays viable: " + Show(on));
        var fits = Chain(Tier(7000, true));
        Check(fits.SequenceEqual(Chain(Tier(7000, false))), "a tier whose plan fits the clock moved: " + Show(fits));
        // The live 75 s deadline binds after the 55 s wall guard: the reserve stays on, exactly as with no deadline.
        var retail = Chain(Tier(70000, true), 75000);
        Check(retail.SequenceEqual(on), "75 s deadline turned the reserve off: " + Show(retail) + " vs " + Show(on));
        Check(Chain(Tier(7000, true), 75000).SequenceEqual(Chain(Tier(7000, false), 75000)), "master moved under the 75 s deadline");
        // Under a hard deadline the first cap is below any plan budget; both tiers keep the old chain, master strictly.
        foreach (int plan in new[] { 7000, 70000 }) {
            var timed = Chain(Tier(plan, true), 15000);
            Check(timed.SequenceEqual(Chain(Tier(plan, false), 15000)), "deadline chain moved at plan " + plan + ": " + Show(timed));
        }
    }
    private static void EliteWallAwarePlanCap()
    {
        var profile = WithLiveBudget(NpcCatalog.All[0], 45, 55);
        // 0.5 ms per node, 2000 lethal-like nodes before the planner, planner wants 400k nodes (200 s of search).
        var rig = new WallRig(profile, 2, .5, 2000); rig.Runner.Start(); rig.Run();
        Check(!rig.Runner.Active && rig.Acted == 2 && !rig.Runner.ClockCut && rig.Stops.All(x => x == "Nodes"),
            "elite+ plans were clock-cut: " + string.Join(",", rig.Stops) + " at " + string.Join(",", rig.ActedAt));
        Check(rig.ActedAt.All(t => t <= 55000) && rig.Caps.Count == 2 && rig.Caps[0] < 45000 * 2 * NpcTurnRunner.NodeRateMargin / 2,
            "elite+ wall-aware caps " + string.Join(",", rig.Caps) + " at " + string.Join(",", rig.ActedAt));
        Check(rig.Runner.SearchShare > .3 && rig.Runner.SearchShare < .5, "measured search share " + rig.Runner.SearchShare);
        Check(rig.Log.Any(l => l.Contains(" turn-summary ") && l.Contains(" searchShare=") && l.Contains(" gcBytes=")
            && l.Contains(" gcCollections=") && l.Contains(" pausedMs=0")), "turn summary lacks share/GC fields");

        // Nozdormu: a 15 s turn deadline clamps the guard to 10 s (5 s reserve); the plan still finishes by nodes.
        var timed = new WallRig(profile, 1, .5, 2000, 15000); timed.Runner.Start(); timed.Run();
        Check(!timed.Runner.ClockCut && timed.Stops.All(x => x == "Nodes") && timed.ActedAt.All(t => t <= 10000),
            "deadline clamp: " + string.Join(",", timed.Stops) + " at " + string.Join(",", timed.ActedAt));

        // A human choice during the NPC turn (Cancel .. Resume) is excluded from the wall guard.
        var paused = new WallRig(profile, 1, .5, 30); paused.Runner.Start();
        for (int i = 0; paused.Runner.State != NpcTurnState.Thinking && i < 1000; i++) { paused.Now += 30; paused.Runner.Tick(); }
        paused.Runner.Cancel(); paused.Now += 30000; paused.Runner.Resume(); paused.Run();
        // Unpaused: ~(55 - ~4 - 1) s x 0.5 share x 2 nodes/ms x 0.8; counting the pause would leave < 18k.
        Check(paused.Acted == 1 && !paused.Runner.ClockCut && paused.Caps.Count == 1 && paused.Caps[0] > 25000
            && paused.Log.Any(l => l.Contains(" turn-summary ") && l.Contains(" pausedMs=30000")),
            "pause counted against the guard: " + string.Join(",", paused.Caps) + " " + paused.Log.LastOrDefault());

        // A running lethal search shares the late-decision slice; it cannot silently extend 300 ms to 1.5 s.
        double now = 0; var lethal = new SteppedJob(() => now += 2.5, 400); bool lethalDone = false;
        var game = new SabberStoneCore.Model.Game(new GameConfig { Player1HeroClass = CardClass.WARRIOR, Player2HeroClass = CardClass.WARRIOR });
        var end = BotAction.From(EndTurnTask.Any(game.Player2)); int acted = 0;
        var late = new NpcTurnRunner(WithLiveBudget(NpcCatalog.All[0], 45, 1), "late-lethal", () => now, () => acted < 1,
            () => new GoldenContext(2, acted, 71, profile), ctx => new LethalFirstJob(end, lethal, () => now += 1),
            reason => { lethalDone = lethal.IsDone; return new GoldenDecision(end, 0, 0, 0, "Clock", reason, null, "fallback"); },
            decision => acted++, () => true);
        late.Start(); now += 2000; // past the 1 s guard before the first decision
        for (int i = 0; late.Active && i < 100000; i++) { now += 30; late.Tick(); }
        Check(acted == 1 && late.ClockCut && !lethalDone && late.SearchMilliseconds <= NpcTurnRunner.LateDecisionMilliseconds + 2.5,
            "lethal exceeded decision deadline: done=" + lethalDone + " search=" + late.SearchMilliseconds);
    }
    private static void SharedLateSearchDeadline()
    {
        double now = 0; int acted = 0;
        var profile = WithLiveBudget(NpcCatalog.All[0], 45, 1);
        var game = new SabberStoneCore.Model.Game(new GameConfig { Player1HeroClass = CardClass.WARRIOR, Player2HeroClass = CardClass.WARRIOR });
        var end = BotAction.From(EndTurnTask.Any(game.Player2));
        DeadlineJob active = null;
        var runner = new NpcTurnRunner(profile, "shared-deadline", () => now, () => acted < 1,
            () => new GoldenContext(2, acted, 71, profile),
            ctx => active = new DeadlineJob(end, () => now += 10),
            reason => throw new Exception("prepared deadline result discarded: " + reason),
            decision => acted++, () => true);
        runner.Start(); now = 2000;
        for (int i = 0; runner.Active && i < 1000; i++) { now += 10; runner.Tick(); }
        Check(acted == 1 && runner.ClockCut && active.Stopped && runner.SearchMilliseconds <= 310,
            "background extended nominal decision: " + runner.SearchMilliseconds);
        Check(active.Window > 0 && active.Window <= 300, "worker got a different extended deadline: " + active.Window);
    }
    // Owner 2026-10-27: late in a long turn a plausible, unfinished lethal search may draw on a 1.5 s pool per NPC turn
    // (not per decision); none once lethal is done, when implausible, into the hard deadline reserve, or under Nozdormu.
    private sealed class PoolRig
    {
        internal double Now; internal int Acted; internal readonly int Actions;
        internal readonly List<bool> LethalDone = new List<bool>(); internal readonly List<double> ActedAt = new List<double>();
        internal readonly List<string> Log = new List<string>();
        internal readonly NpcTurnRunner Runner; internal SteppedJob Lethal;
        internal readonly List<string> Stops = new List<string>();
        internal PoolRig(NpcProfile profile, int actions, int lethalNodes, bool plausible, Func<PoolRig, double> deadlineLeft = null, double nodeCost = 2.5, bool finds = false)
        {
            Actions = actions;
            var game = new SabberStoneCore.Model.Game(new GameConfig { Player1HeroClass = CardClass.WARRIOR, Player2HeroClass = CardClass.WARRIOR });
            var end = BotAction.From(EndTurnTask.Any(game.Player2));
            Runner = new NpcTurnRunner(profile, "late-pool", () => Now, () => Acted < Actions,
                () => new GoldenContext(2, Acted, 71, profile),
                ctx => new LethalFirstJob(end, Lethal = new SteppedJob(() => Now += nodeCost, lethalNodes), () => Now += 1, plausible, finds),
                reason => { LethalDone.Add(Lethal.IsDone); return new GoldenDecision(end, 0, 0, 0, "Clock", reason, null, "fallback"); },
                decision => { ActedAt.Add(Now); Stops.Add(decision.Stop); Acted++; }, () => true, Log.Add,
                turnDeadlineLeft: deadlineLeft == null ? null : (Func<double>)(() => deadlineLeft(this)));
        }
        internal void Turn(double wallBefore)
        {
            Runner.Start(); Now += wallBefore;
            for (int i = 0; Runner.Active && i < 100000; i++) { Now += 30; Runner.Tick(); }
        }
    }
    private static void LateLethalPool()
    {
        var shortGuard = WithLiveBudget(NpcCatalog.All[0], 45, 1);
        double nominal = NpcTurnRunner.LateDecisionMilliseconds - 100, pool = NpcTurnRunner.LateLethalPoolMilliseconds;
        // 400 nodes x 2.5 ms = 1 s of lethal: found inside nominal + pool; only the extension spends the pool.
        var found = new PoolRig(shortGuard, 1, 400, true); found.Turn(2000);
        Check(found.Acted == 1 && found.LethalDone.SequenceEqual(new[] { true }) && found.Runner.LateExtendedDecisions == 1
            && Math.Abs(found.Runner.LatePoolSpentMilliseconds - (found.Runner.SearchMilliseconds - nominal)) < 1e-6
            && found.Runner.SearchMilliseconds <= 1000 + 3.5 && found.Log.Any(l => l.Contains(" latePool=8")),
            "late pool did not finish a plausible lethal: " + found.Runner.SearchMilliseconds + " pool=" + found.Runner.LatePoolSpentMilliseconds);
        // Per turn, not per decision: the second decision only gets what is left, the third none; a new turn refills it.
        var turn = new PoolRig(shortGuard, 3, 400, true); turn.Turn(2000);
        Check(turn.Acted == 3 && turn.LethalDone.SequenceEqual(new[] { true, false, false }) && turn.Runner.LateExtendedDecisions == 2
            && Math.Abs(turn.Runner.LatePoolSpentMilliseconds - pool) < 3.5 && turn.Runner.SearchMilliseconds <= 3 * nominal + pool + 3 * 3.5,
            "late pool is not per turn: done=" + string.Join(",", turn.LethalDone) + " pool=" + turn.Runner.LatePoolSpentMilliseconds
            + " search=" + turn.Runner.SearchMilliseconds);
        turn.Acted = 0; turn.LethalDone.Clear(); turn.Turn(2000);
        Check(turn.Acted == 3 && turn.LethalDone.First() && turn.Runner.LatePoolSpentMilliseconds <= pool + 3.5, "late pool not reset by Start");
        // Implausible lethal, or lethal already finished (None), gets the nominal late slice only.
        var implausible = new PoolRig(shortGuard, 1, 400, false); implausible.Turn(2000);
        var none = new PoolRig(shortGuard, 1, 20, true); none.Turn(2000);
        Check(!implausible.LethalDone[0] && implausible.Runner.SearchMilliseconds <= nominal + 3.5 && implausible.Runner.LateExtendedDecisions == 0,
            "implausible lethal extended: " + implausible.Runner.SearchMilliseconds);
        Check(none.LethalDone[0] && none.Runner.SearchMilliseconds <= nominal + 3.5 && none.Runner.LatePoolSpentMilliseconds == 0,
            "finished lethal still spent the pool: " + none.Runner.SearchMilliseconds);
        // Before the late regime (a long nominal window) the pool is not used.
        var early = new PoolRig(WithLiveBudget(NpcCatalog.All[0], 45, 55), 1, 400, true); early.Turn(0);
        Check(early.LethalDone[0] && early.Runner.LatePoolSpentMilliseconds == 0, "pool spent outside the late regime");
        // Nozdormu (15 s deadline binds before the 55 s guard): unchanged, no extension.
        var elite = WithLiveBudget(NpcCatalog.All[0], 45, 55);
        var nozdormu = new PoolRig(elite, 1, 400, true, r => 15000 - r.Now); nozdormu.Turn(10500);
        Check(!nozdormu.LethalDone[0] && nozdormu.Runner.LateExtendedDecisions == 0 && nozdormu.Runner.SearchMilliseconds <= nominal + 3.5,
            "Nozdormu turn extended: " + nozdormu.Runner.SearchMilliseconds);
        // 75 s rope: past the 55 s guard the pool applies, but never into the 5 s hard-deadline reserve.
        var rope = new PoolRig(elite, 1, 400, true, r => 75000 - r.Now); rope.Turn(56000);
        Check(rope.LethalDone[0] && rope.ActedAt[0] < 70000, "rope turn: done=" + rope.LethalDone[0] + " at " + rope.ActedAt[0]);
        var reserve = new PoolRig(elite, 1, 400, true, r => 75000 - r.Now); reserve.Turn(69300);
        Check(!reserve.LethalDone[0] && reserve.ActedAt[0] <= 70000 + 80 && reserve.Runner.LateExtendedDecisions == 1,
            "extension crossed the hard deadline reserve: done=" + reserve.LethalDone[0] + " at " + reserve.ActedAt[0]);

        // Before the guard the pool is never used, even when the turn search budget leaves a decision under 1.5 s.
        var budgetLate = new PoolRig(WithLiveBudget(NpcCatalog.All[0], .4, 55), 1, 400, true); budgetLate.Turn(0);
        Check(!budgetLate.LethalDone[0] && budgetLate.Runner.LateExtendedDecisions == 0 && budgetLate.Runner.SearchMilliseconds <= 400,
            "pool used before the wall guard: " + budgetLate.Runner.SearchMilliseconds);
        // A lethal proven during an extension is played as a lethal, not logged as a clock cut.
        var proven = new PoolRig(shortGuard, 1, 400, true, finds: true); proven.Turn(2000);
        Check(proven.Acted == 1 && proven.LethalDone.Count == 0 && proven.Stops.SequenceEqual(new[] { "Terminal" }) && !proven.Runner.ClockCut
            && proven.Runner.ClockCuts == 0 && proven.Runner.LateExtendedDecisions == 1 && !proven.Log.Any(l => l.Contains("clock-cut=1")),
            "lethal found in an extension was a clock cut: " + string.Join(",", proven.Stops) + " cuts=" + proven.Runner.ClockCuts);
        // An extension interrupted by a human choice (Cancel .. Resume) still spends the pool.
        var interrupted = new PoolRig(shortGuard, 2, 800, true); interrupted.Runner.Start(); interrupted.Now += 2000;
        for (int i = 0; interrupted.Runner.Active && interrupted.Runner.SearchMilliseconds < nominal + 600 && i < 100000; i++)
        { interrupted.Now += 30; interrupted.Runner.Tick(); }
        double before = interrupted.Runner.SearchMilliseconds;
        interrupted.Runner.Cancel();
        Check(interrupted.Runner.LatePoolSpentMilliseconds >= before - nominal - 1 && interrupted.Runner.LateExtendedDecisions == 1,
            "interrupted extension not charged: pool=" + interrupted.Runner.LatePoolSpentMilliseconds + " search=" + before);
        interrupted.Now += 5000; interrupted.Runner.Resume();
        for (int i = 0; interrupted.Runner.Active && i < 100000; i++) { interrupted.Now += 30; interrupted.Runner.Tick(); }
        Check(interrupted.Runner.LatePoolSpentMilliseconds <= pool + 3.5 && interrupted.Runner.SearchMilliseconds <= 3 * nominal + pool + 3 * 3.5,
            "resumed decision refilled the pool: pool=" + interrupted.Runner.LatePoolSpentMilliseconds + " search=" + interrupted.Runner.SearchMilliseconds);

        // Background workers get the same extended deadline as the owner (never longer), only for a plausible lethal.
        foreach (bool plausible in new[] { false, true })
        {
            double now = 0; int acted = 0; DeadlineJob active = null;
            var game = new SabberStoneCore.Model.Game(new GameConfig { Player1HeroClass = CardClass.WARRIOR, Player2HeroClass = CardClass.WARRIOR });
            var end = BotAction.From(EndTurnTask.Any(game.Player2));
            var runner = new NpcTurnRunner(shortGuard, "pool-deadline", () => now, () => acted < 1, () => new GoldenContext(2, acted, 71, shortGuard),
                ctx => active = new DeadlineJob(end, () => now += 10, plausible), reason => throw new Exception("fallback " + reason),
                decision => acted++, () => true);
            runner.Start(); now = 2000;
            for (int i = 0; runner.Active && i < 1000; i++) { now += 10; runner.Tick(); }
            double limit = plausible ? nominal + pool : nominal;
            Check(acted == 1 && active.Stopped && active.Window > limit - 30 && active.Window <= limit && runner.SearchMilliseconds <= limit + 10,
                "worker deadline (plausible=" + plausible + "): window=" + active.Window + " search=" + runner.SearchMilliseconds);
        }

        // Harness comparison switch: the pre-2026-10-27 per-decision floor gives every late decision ~1.4 s.
        try
        {
            GoldenAblation.LateLethal = GoldenAblation.LateLethalTiming.DecisionFloor;
            // 400 nodes x 5 ms: lethal never finishes inside the floor (the lethal scheduler holds 500 nodes).
            var floor = new PoolRig(shortGuard, 3, 400, true, null, 5); floor.Turn(2000);
            Check(floor.Acted == 3 && floor.Runner.SearchMilliseconds >= 3 * 1395 && floor.Runner.SearchMilliseconds <= 3 * 1406,
                "legacy floor: " + floor.Runner.SearchMilliseconds);
            GoldenAblation.LateLethal = GoldenAblation.LateLethalTiming.SharedDeadline;
            var shared = new PoolRig(shortGuard, 1, 400, true); shared.Turn(2000);
            Check(!shared.LethalDone[0] && shared.Runner.SearchMilliseconds <= nominal + 3.5, "shared deadline variant extended");
        }
        finally { GoldenAblation.Reset(); }
    }
    // Deck parallel lethal: worker wall between frames is charged only while workers run. Workers finish 100 ms in; the
    // planner then needs 1000 ms of 30 ms slices separated by 30 ms frames (~2 s wall). The 1257a643 rule charged it all.
    private static void WorkerWallChargedWhileRunning()
    {
        foreach (var timing in new[] { GoldenAblation.LateLethalTiming.Shipped, GoldenAblation.LateLethalTiming.SharedDeadline })
        {
            try
            {
                GoldenAblation.LateLethal = timing;
                double now = 0; int acted = 0;
                var profile = NpcCatalog.All[0];
                var game = new SabberStoneCore.Model.Game(new GameConfig { Player1HeroClass = CardClass.WARRIOR, Player2HeroClass = CardClass.WARRIOR });
                var end = BotAction.From(EndTurnTask.Any(game.Player2));
                WorkerJob job = null;
                var runner = new NpcTurnRunner(profile, "worker-wall", () => now, () => acted < 1, () => new GoldenContext(2, acted, 71, profile),
                    ctx => job = new WorkerJob(end, () => now, () => now += 1, 1000, 100), reason => throw new Exception("fallback " + reason),
                    decision => acted++, () => true);
                runner.Start();
                for (int i = 0; runner.Active && i < 100000; i++) { now += 30; runner.Tick(); }
                bool shipped = timing == GoldenAblation.LateLethalTiming.Shipped;
                // Shipped: the planner's 1000 ms of slices plus at least the frame gaps while the workers ran (>= 2 x 30 ms).
                Check(acted == 1 && !runner.ClockCut && (shipped ? runner.SearchMilliseconds >= 1000 + 2 * 30 && runner.SearchMilliseconds < 1000 + 4 * 30
                    : runner.SearchMilliseconds > 1800),
                    "worker wall charging (" + timing + "): search=" + runner.SearchMilliseconds + " think wall=" + runner.ThinkWallMilliseconds);
            }
            finally { GoldenAblation.Reset(); }
        }
    }
    // Deck parallel lethal through the world decorators (GuardedSkilledPilot > SkilledPilot): the runner must see the
    // background search, wait for running workers and give them its deadline. Before forwarding, each runner step on a
    // parked search cost a turn node, one slice burned the turn's node budget and later decisions fell to "turn-nodes".
    private static void WrappedSkillPilotWaitsForWorkers()
    {
        var profile = WithLiveBudget(NpcCatalog.All[0], 5, 0);
        var game = new SabberStoneCore.Model.Game(new GameConfig { Player1HeroClass = CardClass.WARRIOR, Player2HeroClass = CardClass.WARRIOR,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            StartPlayer = 2, SkipMulligan = true, Shuffle = false });
        game.StartGame();
        var view = FairBotState.Create(game, 2, 7, DeckStyle.Tempo);
        var end = BotAction.From(EndTurnTask.Any(game.Player2));
        double now = 0; int acted = 0; var fallbacks = new List<string>(); var stops = new List<string>(); var log = new List<string>();
        var jobs = new List<ParkedJob>();
        var inner = new ParkedPilot(() => { var j = new ParkedJob(end, () => now, () => now += 1, 100, 300); jobs.Add(j); return j; });
        var perfect = new SkillProfile("test", 1, 0, 1, profile.Budget, 0, 1, 1, new MistakeRule[0]);
        var wrapped = new GuardedSkilledPilot("npc.test", new SkilledPilot(inner, null, perfect, 1), inner);
        var first = wrapped.Begin(view, new GoldenContext(2, 0, 71, profile));
        Check(first is INpcBackgroundSearch b && b.BackgroundClock && first is INpcLethalBound bound && bound.LethalPlausible,
            "decorated job hides the background search or lethal bound");
        jobs.Clear();
        var runner = new NpcTurnRunner(profile, "wrapped-workers", () => now, () => acted < 3, () => new GoldenContext(2, acted, 71, profile),
            ctx => wrapped.Begin(view, ctx), reason => { fallbacks.Add(reason); return new GoldenDecision(end, 0, 0, 0, "Clock", reason, null, "fallback"); },
            decision => { stops.Add(decision.Stop); acted++; }, () => true, log.Add, turnNodes: 2000);
        runner.Start();
        for (int i = 0; runner.Active && i < 100000; i++) { now += 30; runner.Tick(); }
        Check(acted == 3 && fallbacks.Count == 0 && stops.All(x => x == "Nodes") && runner.TurnSearchNodes <= 3 * 100 + 10,
            "decorated decisions burned the turn budget: nodes=" + runner.TurnSearchNodes + " stops=" + string.Join(",", stops)
            + " fallbacks=" + string.Join(",", fallbacks));
        Check(jobs.Count == 3 && jobs.All(j => j.Limit > 0 && j.Limit <= NpcTurnRunner.TurnSearchMilliseconds) && log.Count(l => l.Contains("lethalWorkers=2")) == 3,
            "decorated workers got no runner deadline: " + string.Join(",", jobs.Select(j => j.Limit)));
    }
    private sealed class ParkedPilot : IFrameSlicedGoldenPilot
    {
        private readonly Func<IGoldenDecisionJob> next;
        internal ParkedPilot(Func<IGoldenDecisionJob> next) { this.next = next; }
        public IGoldenDecisionJob Begin(FairBotState view, GoldenContext context) => next();
        public GoldenDecision Decide(FairBotState view, GoldenContext ctx) => throw new NotSupportedException();
        public IReadOnlyList<int> Mulligan(IGoldenMulliganView view, GoldenContext ctx) => new int[0];
    }
    // Like GoldenDecisionJob with parallel lethal: the planner finishes first, then StepNode parks (returns without
    // progress) until the workers finish; a search stopped by nodes before any planner node reports "turn-nodes".
    private sealed class ParkedJob : IGoldenDecisionJob, INpcBackgroundSearch, INpcSearchContinuation, INpcLethalBound
    {
        private readonly BotAction action; private readonly Func<double> clock; private readonly Action step;
        private readonly int plannerNodes; private readonly double runFor; private double started = -1; private int planned; private bool stopped;
        internal double Limit = -1;
        internal ParkedJob(BotAction action, Func<double> clock, Action step, int plannerNodes, double runFor)
        { this.action = action; this.clock = clock; this.step = step; this.plannerNodes = plannerNodes; this.runFor = runFor; }
        public bool LethalPlausible => true;
        public int BackgroundNodes => 0;
        public bool Waiting => BackgroundRunning && planned >= plannerNodes;
        public bool BackgroundClock => true;
        public bool BackgroundRunning => !stopped && (started < 0 || clock() - started < runFor);
        public void LimitBackground(double milliseconds) { Limit = milliseconds; }
        public void PollBackground() { if (started < 0) started = clock(); }
        public void StopBackground() { stopped = true; }
        public bool IsDone => planned >= plannerNodes && !BackgroundRunning;
        public GoldenDecision Result => new GoldenDecision(action, 0, planned, 1, "Nodes", null, null);
        public string LethalResult => BackgroundRunning ? "incomplete" : "none";
        public bool PlanReused => false;
        public void StepNode() { if (started < 0) started = clock(); if (planned < plannerNodes) { planned++; step(); } }
        public GoldenDecision StopSearch(SearchStop stop)
        {
            StopBackground();
            return planned == 0 ? new GoldenDecision(action, 0, 0, 0, stop.ToString(), "turn-nodes", null, "fallback")
                : new GoldenDecision(action, 0, planned, 1, stop.ToString(), null, null);
        }
    }
    private sealed class WorkerJob : IGoldenDecisionJob, INpcBackgroundSearch
    {
        private readonly BotAction action; private readonly Func<double> clock; private readonly Action step;
        private readonly int plannerNodes; private readonly double runFor; private double started = -1; private int planned;
        internal WorkerJob(BotAction action, Func<double> clock, Action step, int plannerNodes, double runFor)
        { this.action = action; this.clock = clock; this.step = step; this.plannerNodes = plannerNodes; this.runFor = runFor; }
        public int BackgroundNodes => 0;
        public bool Waiting => BackgroundRunning && planned >= plannerNodes;
        public bool BackgroundClock => true;
        public bool BackgroundRunning => started < 0 || clock() - started < runFor;
        public void LimitBackground(double milliseconds) { }
        public void PollBackground() { if (started < 0) started = clock(); }
        public void StopBackground() { }
        public bool IsDone => planned >= plannerNodes && !BackgroundRunning;
        public GoldenDecision Result => new GoldenDecision(action, 0, planned, 1, "Nodes", null, null);
        public string LethalResult => "none";
        public bool PlanReused => false;
        public void StepNode() { if (started < 0) started = clock(); if (planned < plannerNodes) { planned++; step(); } }
    }
    private sealed class DeadlineJob : IGoldenDecisionJob, INpcBackgroundSearch, INpcSearchContinuation, INpcLethalBound
    {
        private readonly BotAction action; private readonly Action advance;
        internal double Window; internal bool Stopped;
        internal DeadlineJob(BotAction action, Action advance, bool plausible = false) { this.action = action; this.advance = advance; LethalPlausible = plausible; }
        public bool LethalPlausible { get; }
        public int BackgroundNodes { get; private set; }
        public bool Waiting => !Stopped;
        public bool BackgroundClock => true;
        public bool BackgroundRunning => !Stopped;
        public void LimitBackground(double milliseconds) { Window = milliseconds; }
        public void PollBackground() { BackgroundNodes++; advance(); }
        public void StopBackground() { Stopped = true; }
        public bool IsDone => Stopped;
        public GoldenDecision Result => new GoldenDecision(action, 0, BackgroundNodes, 1, "Clock", null, null);
        public string LethalResult => "incomplete";
        public bool PlanReused => false;
        public void StepNode() { advance(); }
        public GoldenDecision StopSearch(SearchStop stop) { StopBackground(); return Result; }
    }
    private sealed class SteppedJob : INodeJob
    {
        private readonly Action step; private readonly int count; private int nodes;
        internal SteppedJob(Action step, int count) { this.step = step; this.count = count; }
        public bool IsDone => nodes >= count;
        public void StepNode() { nodes++; step(); }
    }
    // Lethal search first (INpcLethalSearch), then a planner that never finishes on its own.
    // finds: a finished lethal search is a proven lethal; the next step completes the decision with it.
    private sealed class LethalFirstJob : IGoldenDecisionJob, INpcLethalSearch, INpcLethalBound
    {
        private readonly BotAction action; private readonly Action step; private readonly bool finds;
        internal LethalFirstJob(BotAction action, INodeJob lethal, Action step, bool plausible = false, bool finds = false)
        { this.action = action; LethalSearch = lethal; this.step = step; LethalPlausible = plausible; this.finds = finds; }
        public bool LethalPlausible { get; }
        public INodeJob LethalSearch { get; }
        public bool IsDone { get; private set; }
        public GoldenDecision Result => IsDone ? new GoldenDecision(action, 1000000, 1, 1, "Terminal", null, null) : null;
        public string LethalResult => LethalSearch.IsDone ? finds ? "found" : "none" : "incomplete";
        public bool PlanReused => false;
        public void StepNode() { if (finds && LethalSearch.IsDone) IsDone = true; else step(); }
    }

    private static void SearchClockBoundary()
    {
        Check(NpcTurnRunner.TurnSearchMilliseconds == 5000, "turn search limit changed");
        Check(NpcCatalog.All.All(p => p.Budget.TurnNodes == 28000), "golden turn budget changed");
        var rig = new Rig(WithLiveBudget(NpcCatalog.All[0], 5, 0)) { NodeCost = 1000 };
        var totals = new List<double>();
        for (int i = 0; rig.Actions.Count < 5 && i < 400; i++)
        {
            int before = rig.Actions.Count; rig.Now += 10000; rig.Runner.Tick();
            if (rig.Actions.Count != before) totals.Add(rig.Runner.SearchMilliseconds);
        }
        // The 5 s turn budget, then the 300 ms late searches (each 1 s node overshoots it).
        Check(totals.Take(5).SequenceEqual(new double[] { 5000, 6000, 7000, 8000, 9000 }), "turn clock: " + string.Join(",", totals));
        Check(rig.Runner.ClockCuts == 5 && rig.Fallbacks.All(f => f == "clock-cut"), "clock cuts per decision");
        var fast = new Rig(WithLiveBudget(NpcCatalog.All[0], 5, 0)) { NodeCost = 285 };
        for (int i = 0; fast.Actions.Count < 2 && i < 400; i++) { fast.Now += 10000; fast.Runner.Tick(); }
        Check(fast.Actions.Count == 2 && !fast.Runner.ClockCut && fast.Fallbacks.Count == 0 && fast.Runner.SearchMilliseconds == 3990,
            "two 2 s searches are within the turn budget");
    }

    // Before the planner starts the runner sizes its node allowance from the measured node rate, so searches finish
    // by nodes inside the clock; later decisions shrink as the turn budget runs down. No cut, no fallback.
    private static void LivePlanNodeCap()
    {
        var profile = WithLiveBudget(NpcCatalog.All[0], 5, 0); double now = 0; int acted = 0; var caps = new List<int>(); var log = new List<string>();
        var game = new SabberStoneCore.Model.Game(new GameConfig { Player1HeroClass = CardClass.WARRIOR, Player2HeroClass = CardClass.WARRIOR });
        var action = BotAction.From(EndTurnTask.Any(game.Player2));
        var runner = new NpcTurnRunner(profile, "plan-cap", () => now, () => acted < 6,
            () => new GoldenContext(2, acted, 71, profile),
            ctx => new CapJob(action, () => now += 1, caps),
            reason => throw new Exception("fallback " + reason),
            decision => { Check(decision.Stop == "Nodes", "capped decision stop"); acted++; }, () => true, log.Add);
        runner.Start();
        for (int i = 0; runner.Active && i < 100000; i++) { now += 16; runner.Tick(); }
        Check(!runner.Active && acted == 6 && !runner.ClockCut && runner.ClockCuts == 0, "capped searches were clock-cut");
        // Rate 1 node/ms: 0.8 x (5000 - 30 - 1000 reserve), then 0.8 x what is left of the turn, then the late floor.
        Check(caps.Count == 6 && caps[0] == 3176 && caps[1] == 1411 && caps[2] == 258 && caps.Skip(3).All(c => c == 216),
            "plan caps " + string.Join(",", caps));
        Check(runner.CappedDecisions == 6 && Math.Abs(runner.NodeRate - 1) < 1e-9, "node rate " + runner.NodeRate);
        Check(log.Any(line => line.Contains(" turn-summary ") && line.Contains("cappedDecisions=6") && line.Contains("clockCut=0")),
            "turn summary omitted caps");
        // A fast client (2.5 nodes/ms) gets the full plan budget on its first decision.
        now = 0; acted = 0; caps.Clear();
        var quick = new NpcTurnRunner(profile, "plan-cap-fast", () => now, () => acted < 1,
            () => new GoldenContext(2, acted, 71, profile), ctx => new CapJob(action, () => now += .4, caps),
            reason => throw new Exception("fallback " + reason), decision => acted++, () => true);
        quick.Start();
        for (int i = 0; quick.Active && i < 100000; i++) { now += 16; quick.Tick(); }
        Check(caps.Count == 1 && caps[0] >= profile.Budget.PlanNodes && quick.CappedDecisions == 0, "fast client cap " + string.Join(",", caps));

        // Node-budget path: a GoldenDecisionJob without a live cap searches exactly as before; a cap bounds the planner.
        var deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList();
        var real = new SabberStoneCore.Model.Game(new GameConfig { Player1HeroClass = CardClass.WARRIOR, Player2HeroClass = CardClass.WARRIOR,
            Player1Deck = deck, Player2Deck = deck.ToList(), StartPlayer = 2, SkipMulligan = true, Shuffle = false });
        real.StartGame();
        Func<Func<int>, GoldenDecision> decide = cap => {
            var pilot = new GoldenPilot(profile);
            var job = (GoldenDecisionJob)pilot.Begin(pilot.Options.FairViewFactory(real, 2, 7, DeckStyle.Tempo), new GoldenContext(real.Turn, 1, 7, profile));
            if (cap != null) job.CapPlanNodes(cap);
            for (int i = 0; !job.IsDone && i < 100000; i++) job.StepNode();
            return job.Result;
        };
        GoldenDecision plain = decide(null), again = decide(null), uncapped = decide(() => -1), capped = decide(() => 40);
        Check(plain.Nodes == again.Nodes && plain.Action.Equals(again.Action) && uncapped.Nodes == plain.Nodes
            && uncapped.Action.Equals(plain.Action), "uncapped golden decision changed");
        Check(capped.Nodes < plain.Nodes && capped.Nodes <= 40 + profile.Budget.LethalNodes + 2, "live cap ignored: " + capped.Nodes + " vs " + plain.Nodes);
    }
    // Idle board: 30 ms of search per frame, more while the rest of the frame keeps 25 fps; animating board: 10 ms.
    private static void AdaptiveSlices()
    {
        var slow = new SliceRig(true, 60); var fast = new SliceRig(true, 5); var zero = new SliceRig(true, 0); var busy = new SliceRig(false, 5);
        Check(slow.Slices.Skip(1).All(x => x == 30) && slow.Slices[0] == 30, "slow idle slices " + string.Join(",", slow.Slices));
        Check(fast.Slices[0] == 30 && fast.Slices.Skip(2).All(x => x == 35), "fast idle slices " + string.Join(",", fast.Slices));
        Check(zero.Slices.Skip(2).All(x => x == 40), "max idle slice " + string.Join(",", zero.Slices));
        Check(busy.Slices.All(x => x == 10), "animating slices " + string.Join(",", busy.Slices));
        Check(slow.Log.Any(line => line.Contains("msPerFrame=") && line.Contains("frameOtherMs=60.0")), "frame telemetry missing");
    }
    // One long search (1 ms per node, 400 nodes) with a fixed non-search frame time between ticks.
    private sealed class SliceRig
    {
        internal readonly List<double> Slices = new List<double>();
        internal readonly List<string> Log = new List<string>();
        internal SliceRig(bool idle, double other)
        {
            var profile = WithLiveBudget(NpcCatalog.All[0], 5, 0); double now = 0; int acted = 0;
            var game = new SabberStoneCore.Model.Game(new GameConfig { Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.MAGE });
            var action = BotAction.From(EndTurnTask.Any(game.Player2));
            var runner = new NpcTurnRunner(profile, "slices", () => now, () => acted < 1, () => new GoldenContext(2, acted, 71, profile),
                ctx => new CapJob(action, () => now += 1, new List<int>(), 400), reason => throw new Exception("fallback " + reason),
                decision => acted++, () => idle, Log.Add);
            runner.Start();
            while (runner.Active && runner.State != NpcTurnState.Thinking) { now += 16; runner.Tick(); }
            while (runner.Active && Slices.Count < 50)
            {
                double before = runner.SearchMilliseconds; runner.Tick();
                if (runner.SearchMilliseconds > before && runner.State == NpcTurnState.Thinking) Slices.Add(runner.SearchMilliseconds - before);
                now += runner.State == NpcTurnState.Thinking ? other : 16;
            }
        }
    }
    // Every decision line carries heap and deepest-line telemetry; one crossing NpcDecisionCost logs one WARNING line.
    // A real golden decision reports the deepest line its searches built.
    private static void DecisionCostLog()
    {
        var profile = WithLiveBudget(NpcCatalog.All[0], 5, 0); double now = 0; int acted = 0; var log = new List<string>();
        var game = new SabberStoneCore.Model.Game(new GameConfig { Player1HeroClass = CardClass.WARRIOR, Player2HeroClass = CardClass.WARRIOR });
        var action = BotAction.From(EndTurnTask.Any(game.Player2));
        var runner = new NpcTurnRunner(profile, "decision-cost", () => now, () => acted < 2,
            () => new GoldenContext(2, acted, 71, profile), ctx => new DepthJob(action, acted == 0 ? NpcDecisionCost.OutlierDepth + 2 : 3),
            reason => throw new Exception("fallback " + reason), decision => acted++, () => true, log.Add);
        runner.Start();
        for (int i = 0; runner.Active && i < 100000; i++) { now += 16; runner.Tick(); }
        var decisions = log.Where(l => l.Contains(" action=") && !l.Contains(" WARNING ")).ToArray();
        var warnings = log.Where(l => l.Contains(" WARNING decision-cost=")).ToArray();
        Check(acted == 2 && decisions.Length == 2 && decisions.All(l => l.Contains(" heapMB=") && l.Contains(" heapGrowthMB=")), "decision cost fields: " + string.Join(" | ", log));
        Check(decisions[0].Contains(" deepest=50") && decisions[1].Contains(" deepest=3"), "decision deepest: " + string.Join(" | ", decisions));
        Check(warnings.Length == 1 && warnings[0].Contains("decision-cost=depth turn=2 turnDecision=1 ") && warnings[0].Contains(" deepest=50 "),
            "decision cost warning: " + string.Join(" | ", warnings));

        var deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList();
        var real = new SabberStoneCore.Model.Game(new GameConfig { Player1HeroClass = CardClass.WARRIOR, Player2HeroClass = CardClass.WARRIOR,
            Player1Deck = deck, Player2Deck = deck.ToList(), StartPlayer = 2, SkipMulligan = true, Shuffle = false });
        real.StartGame();
        var pilot = new GoldenPilot(profile);
        var job = (GoldenDecisionJob)pilot.Begin(pilot.Options.FairViewFactory(real, 2, 7, DeckStyle.Tempo), new GoldenContext(real.Turn, 1, 7, profile));
        while (!job.IsDone) job.StepNode();
        Check(job.Deepest > 0 && pilot.LastDeepest == job.Deepest && job.Deepest < NpcDecisionCost.OutlierDepth, "golden deepest " + job.Deepest + "/" + pilot.LastDeepest);
    }
    private sealed class DepthJob : IGoldenDecisionJob, INpcSearchDepth
    {
        private readonly BotAction action;
        internal DepthJob(BotAction action, int deepest) { this.action = action; Deepest = deepest; }
        public int Deepest { get; }
        public bool IsDone { get; private set; }
        public GoldenDecision Result { get; private set; }
        public string LethalResult => "none";
        public bool PlanReused => false;
        public void StepNode() { IsDone = true; Result = new GoldenDecision(action, 0, 1, 1, "Exhausted", null, null); }
    }

    private sealed class CapJob : IGoldenDecisionJob, INpcPlanNodeCap
    {
        private readonly BotAction action; private readonly Action step; private readonly List<int> caps;
        private Func<int> cap; private int nodes, allowance = -1; private readonly int uncapped;
        private readonly int before;
        internal CapJob(BotAction action, Action step, List<int> caps, int uncapped = 5000, int before = 30)
        { this.action = action; this.step = step; this.caps = caps; this.uncapped = uncapped; this.before = before; }
        public void CapPlanNodes(Func<int> cap) { this.cap = cap; }
        public bool IsDone { get; private set; }
        public GoldenDecision Result { get; private set; }
        public string LethalResult => "none";
        public bool PlanReused => false;
        // `before` (30) lethal-like nodes, then the planner starts and asks for its allowance (uncapped: 5000 nodes).
        public void StepNode()
        {
            nodes++; step();
            if (nodes == before) { allowance = cap?.Invoke() ?? -1; caps.Add(allowance); if (allowance < 0 || allowance > uncapped) allowance = uncapped; }
            if (allowance >= 0 && nodes >= before + allowance) { IsDone = true; Result = new GoldenDecision(action, 0, nodes, 1, "Nodes", null, null); }
        }
    }

    private static void ClockCutUsesContinuations()
    {
        var profile = WithLiveBudget(NpcCatalog.All[0], 5, 0); double now = 0; int acted = 0, expanded = 0, stopped = 0, fallback = 0;
        var game = new SabberStoneCore.Model.Game(new GameConfig { Player1HeroClass = CardClass.WARRIOR, Player2HeroClass = CardClass.WARRIOR });
        var action = BotAction.From(EndTurnTask.Any(game.Player2));
        var runner = new NpcTurnRunner(profile, "cut-plan", () => now, () => acted < 3,
            () => new GoldenContext(2, acted, 71, profile),
            ctx => new ContinuationJob(action, () => { expanded++; now += 30000; }, () => stopped++),
            reason => { fallback++; throw new Exception("discarded searched continuation"); },
            decision => { Check(decision.Stop == "Clock" && decision.FallbackReason == null, "cut plan trace"); acted++; }, () => true);
        runner.Start(); runner.RunSynchronously();
        Check(runner.ClockCut && runner.ClockCuts == 3 && acted == 3 && stopped == 3 && expanded == 3 && fallback == 0,
            "each cut decision must take the searched continuation, never the fallback");
        now = 0; acted = 0; fallback = 0;
        var fallbackRunner = new NpcTurnRunner(profile, "empty-cut", () => now, () => acted < 1,
            () => new GoldenContext(2, 1, 7, profile),
            ctx => new ContinuationJob(action, () => now += 30000, () => { }, true),
            reason => { fallback++; return new GoldenDecision(action, 0, 0, 0, "Clock", reason, null, "fallback"); },
            decision => acted++, () => true);
        fallbackRunner.Start(); fallbackRunner.RunSynchronously();
        Check(fallback == 1 && acted == 1, "empty clock-cut continuation uses host safe fallback");
    }
    private sealed class ContinuationJob : IGoldenDecisionJob, INpcSearchContinuation
    {
        private readonly BotAction action;
        private readonly Action expand, stop; private readonly bool empty;
        internal ContinuationJob(BotAction action, Action expand, Action stop, bool empty = false) { this.action = action; this.expand = expand; this.stop = stop; this.empty = empty; }
        public bool IsDone { get; private set; }
        public GoldenDecision Result { get; private set; }
        public string LethalResult => "incomplete";
        public bool PlanReused => true;
        public void StepNode() { expand(); }
        public GoldenDecision StopSearch(SearchStop reason)
        { stop(); IsDone = true; return Result = new GoldenDecision(action, 0, 1, 1, reason.ToString(), empty ? "clock-cut" : null, null, empty ? "fallback" : "search"); }
    }

    private static void ReachIdleWait(Rig rig)
    {
        for (int i = 0; rig.Runner.State != NpcTurnState.WaitForIdle && i < 1000; i++)
        { rig.Now += 10; rig.Runner.Tick(); }
        Check(rig.Runner.State == NpcTurnState.WaitForIdle, "runner never checked idle");
    }

    private static void BusyIdleWaitsPerAction()
    {
        var rig = new Rig(WithLiveBudget(NpcCatalog.All[0], 5, 0)) { Idle = false, NodeCost = 0, Busy = "big-card" };
        for (int action = 0; action < 3; action++)
        {
            ReachIdleWait(rig);
            Check(rig.Actions.Count == action, "acted before checking idle");
            double started = rig.Now;
            rig.Now = started + NpcTurnRunner.IdleWaitMilliseconds - 1; rig.Runner.Tick();
            Check(rig.Actions.Count == action && rig.Runner.State == NpcTurnState.WaitForIdle,
                "busy client did not delay action for four seconds");
            rig.Now = started + NpcTurnRunner.IdleWaitMilliseconds; rig.Runner.Tick();
            Check(rig.Actions.Count == action + 1 && rig.Runner.IdleTimedOut,
                "busy client delayed action beyond four seconds");
            Check(rig.Log.Count(line => line.Contains("idle-timeout")) == action + 1,
                "action timeout not logged once");
            Check(rig.Log.Last(line => line.Contains("idle-timeout")).EndsWith(" busy=big-card", StringComparison.Ordinal),
                "timeout must name the busy native condition");
            Check(rig.Runner.IdleTimeouts == action + 1 && rig.Runner.IdleWaits == 0, "timeout accounting");
        }
    }

    // Live 2026-10-25 target: on a settled board the idle wait ends on the first tick, so the turn's wall time is the
    // personality pacing (PreTurn/Action pauses) plus search, nothing else; the turn summary line reports it.
    private static void IdleBoardHasNoWaitOverhead()
    {
        foreach (var profile in NpcCatalog.All)
        {
            var rig = new Rig(profile) { NodeCost = 0 };
            double frame = 16;
            rig.Run(false, frame);
            var runner = rig.Runner;
            Check(runner.IdleTimeouts == 0 && runner.IdleWaits == rig.Actions.Count
                && runner.IdleWaitedMilliseconds <= frame * runner.IdleWaits,
                profile.Id + ": idle board waited " + runner.IdleWaitedMilliseconds + " ms over " + runner.IdleWaits + " waits");
            // One frame per state change at most: pre-turn, then per action pause/think/act.
            double overhead = rig.Now - runner.ArtificialDelayMilliseconds;
            Check(overhead <= frame * (4 * rig.Actions.Count + 4), profile.Id + ": per-action overhead " + overhead + " ms on an idle board");
            var summary = rig.Log.SingleOrDefault(line => line.Contains(" turn-summary "));
            Check(summary != null && summary.Contains("actions=" + rig.Actions.Count) && summary.Contains("idleTimeouts=0"),
                profile.Id + ": turn summary " + summary);
        }
    }

    private static void IdleRecoversAfterTimeout()
    {
        var rig = new Rig(WithLiveBudget(NpcCatalog.All[0], 5, 0)) { Idle = false, NodeCost = 0 };
        ReachIdleWait(rig);
        rig.Now += NpcTurnRunner.IdleWaitMilliseconds; rig.Runner.Tick();
        Check(rig.Actions.Count == 1, "timeout did not release first action");
        ReachIdleWait(rig);
        rig.Now += 1000; rig.Runner.Tick();
        Check(rig.Actions.Count == 1, "timeout bypassed next idle check");
        rig.Idle = true; rig.Runner.Tick();
        Check(rig.Actions.Count == 2, "recovered idle did not release next action");
        Check(rig.Log.Count(line => line.Contains("idle-timeout")) == 1, "recovered idle logged timeout");
    }

    private static void FreshTurnChecksIdle()
    {
        var rig = new Rig(WithLiveBudget(NpcCatalog.All[0], 5, 0)) { Idle = false, NodeCost = 0 };
        ReachIdleWait(rig);
        rig.Now += NpcTurnRunner.IdleWaitMilliseconds; rig.Runner.Tick();
        Check(rig.Runner.IdleTimedOut, "fixture did not time out");
        rig.Runner.Cancel(); rig.Actions.Clear(); rig.Runner.Start();
        Check(!rig.Runner.IdleTimedOut, "fresh turn retained idle timeout");
        ReachIdleWait(rig);
        rig.Now += NpcTurnRunner.IdleWaitMilliseconds - 1; rig.Runner.Tick();
        Check(rig.Actions.Count == 0, "fresh turn bypassed idle check");
        rig.Idle = true; rig.Runner.Tick();
        Check(rig.Actions.Count == 1 && !rig.Runner.IdleTimedOut, "fresh turn did not resume on idle");
    }

    private static void NativePublicInputs()
    {
        var previous = NpcPilots.Factory;
        try
        {
            NpcPilots.Factory = null;
            var rig = new NativeRig(NpcCatalog.All[0].DeckId, false);
            var game = rig.Match.Game; var human = game.Player1; var bot = game.Player2;
            human.BaseMana = 10; human.UsedMana = 0;
            var card = human.DeckZone.First(c => c.Card.Type == CardType.MINION);
            human.DeckZone.Remove(card); human.HandZone.Add(card);
            Check(game.Process(human.Options().First(t => t.Source?.Id == card.Id)), "public play fixture failed");
            rig.EndHumanTurn(false);
            var context = new GoldenContext(game.Turn, 1, 7, NpcCatalog.All[0]);
            var job = rig.Match.BeginNpcDecision(context) as GoldenDecisionJob;
            Check(job != null && job.PublicInputs.OpponentPlayed.Contains(card.Card.Id), "native host omitted public opponent plays");

            var source = SabberStoneCore.Model.Entities.Entity.FromCard(bot, Cards.FromId("EDR_456"), zone: bot.GraveyardZone);
            var one = SabberStoneCore.Model.Entities.Entity.FromCard(bot, Cards.FromId("CS2_231"), zone: bot.SetasideZone);
            var two = SabberStoneCore.Model.Entities.Entity.FromCard(bot, Cards.FromId("CS2_182"), zone: bot.SetasideZone);
            bot.Choice = new Choice(bot) { SourceId = source.Id, ChoiceType = ChoiceType.GENERAL,
                ChoiceAction = ChoiceAction.HAND, Choices = new List<int> { one.Id, two.Id } };
            job = rig.Match.BeginNpcDecision(context) as GoldenDecisionJob;
            Check(job != null && job.PublicInputs.DiscoverSource == "EDR_456"
                && job.PublicInputs.Offered.Select(p => p.Key).SequenceEqual(new[] { one.Id, two.Id }),
                "native host omitted discover source/offers");
            rig.Match.StopNpc();
        }
        finally { NpcPilots.Factory = previous; }
    }

    private static void ThinkingHoverFrames()
    {
        var previous = NpcPilots.Factory;
        try
        {
            NpcPilots.Factory = null;
            var rig = new NativeRig(NpcCatalog.All[0].DeckId, false, clockStep: .1, presentation: true);
            SabberStoneCore.Model.Entities.Entity.FromCard(rig.Match.Game.Player2, Cards.FromId("CS2_231"), zone: rig.Match.Game.Player2.HandZone);
            rig.EndHumanTurn(false); rig.Match.NpcRunner.SliceMilliseconds = 1; rig.Match.NpcRunner.IdleSliceMilliseconds = 1; rig.Match.NpcRunner.MaxIdleSliceMilliseconds = 1;
            bool thinkingHover = false;
            for (int i = 0; i < 10000 && rig.Record.Decisions == 0; i++)
            {
                rig.Now += 50; rig.Match.TickNpc();
                thinkingHover |= rig.Match.NpcRunner.State == NpcTurnState.Thinking
                    && rig.UserUi.Any(p => p.Value.HasMouseInfo && p.Value.MouseInfo.OverCard > 0);
            }
            Check(thinkingHover, "no real GoldenPilot hover during Thinking frames");
            int source = rig.Record.LastDecision.Action.Source;
            var hovers = rig.UserUi.Where(p => p.Value.HasMouseInfo).ToArray();
            Check(source > 0 && hovers.Last().Value.MouseInfo.OverCard == 0, "acted card hover not cleared");
            var focus = hovers.Last(p => p.Value.MouseInfo.OverCard == source);
            Check(hovers.Last().Key - focus.Key >= 300, "acted card lacked 300ms dwell");
            var changes = hovers.Where(p => p.Value.MouseInfo.OverCard > 0).ToArray();
            Check(changes.Zip(changes.Skip(1), (a, b) => b.Key - a.Key).All(ms => ms >= 600), "hover changed within 600ms");
            Check(rig.Match.NpcRunner.ArtificialDelayMilliseconds <= 4000, "hover exceeded artificial delay cap");

            var legacy = new NativeRig("mage", false, presentation: true);
            legacy.EndHumanTurn();
            Check(legacy.Match.NpcRunner == null && legacy.UserUi.Count == 0, "legacy opponent emitted NPC presentation");
        }
        finally { NpcPilots.Factory = previous; }
    }

    private static void DeckOnlyLethalStartupMode()
    {
        string run = "4d93167d-fc51-4ba0-a79f-64e524ce5a27";
        try
        {
            var deck = OfflineGuard.ResolveLaunch(@"STEAMDECK\steamuser", "STEAMDECK",
                @"C:\hs-native-ui-"+@"prototype\client\Hearthstone.exe", @"C:\users\steamuser\AppData\Local",
                new[] { "-hs-native-ui-run", run, "-hs-native-ui-host", "deck" }, false, true);
            GoldenPilot.ConfigureLethalHost(deck.Deck);
            Check(GoldenPilot.ParallelLethalEnabled, "validated Deck did not enable parallel lethal");
            foreach (int lane in new[] { 0, 1, 2, 3 })
            {
                var windows = OfflineGuard.ResolveLaunch(@"LAB\HSNativeUIProbe", "LAB",
                    OfflineGuard.LaneRoot(lane) + @"\client\Hearthstone.exe", LabTestPaths.LocalData,
                    new[] { "-hs-native-ui-run", run, "-hs-native-ui-lane", lane.ToString() }, false);
                GoldenPilot.ConfigureLethalHost(windows.Deck);
                Check(!GoldenPilot.ParallelLethalEnabled, "Windows lane silently enabled parallel lethal");
            }
        }
        finally { GoldenPilot.ParallelLethalEnabled = false; }
    }

    private static void LethalOutsideTurnBudget()
    {
        try
        {
            foreach (bool parallel in new[] { false, true })
            { GoldenPilot.ParallelLethalEnabled = parallel; LethalOutsideTurnBudgetMode(); }
        }
        finally { GoldenPilot.ParallelLethalEnabled = false; }
    }
    private static void LethalOutsideTurnBudgetMode()
    {
        var deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList();
        var game = new SabberStoneCore.Model.Game(new GameConfig { Player1HeroClass = CardClass.WARRIOR, Player2HeroClass = CardClass.WARRIOR,
            Player1Deck = deck, Player2Deck = deck.ToList(), StartPlayer = 2, SkipMulligan = true, Shuffle = false });
        game.StartGame();
        game.Player1.Hero.Damage = 29;
        var minion = (SabberStoneCore.Model.Entities.Minion)SabberStoneCore.Model.Entities.Entity.FromCard(game.Player2,
            Cards.FromId("CS2_182"), zone: game.Player2.BoardZone);
        minion.IsExhausted = false;
        var profile = NpcCatalog.All[0]; var pilot = new GoldenPilot(profile);
        GoldenDecision result = null; int fallback = 0; var modeLog = new List<string>();
        var runner = new NpcTurnRunner(profile, "lethal-no-turn-nodes", () => 0, () => result == null,
            () => new GoldenContext(game.Turn, 1, 7, profile),
            context => pilot.Begin(pilot.Options.FairViewFactory(game, 2, 7, DeckStyle.Tempo), context),
            reason => { fallback++; return new GoldenDecision(BotAction.From(EndTurnTask.Any(game.Player2)), 0, 0, 0, "Forced", reason, null, "fallback"); },
            decision => result = decision, () => true, log: modeLog.Add, turnNodes: 0);
        runner.Start(); runner.RunSynchronously();
        Check(fallback == 0 && result != null && result.Action.Type == PlayerTaskType.MINION_ATTACK
            && result.Stop == "Terminal", "turn budget skipped independently capped lethal search");
        Check(modeLog.Any(line => line.Contains("lethalWorkers=" + (GoldenPilot.ParallelLethalEnabled ? 2 : 1))),
            "decision telemetry omitted actual lethal worker mode");
    }

    private static void RunnerFailures()
    {
        var failed = new Rig(WithLiveBudget(NpcCatalog.All[0], 5, 0)) { ThrowAct = true };
        failed.Runner.Tick(true); failed.Runner.Tick(true);
        try { failed.Runner.Tick(true); } catch (IOException) { }
        Check(failed.Runner.State != NpcTurnState.Act, "failed action retained for retry");
        failed.Runner.Tick(true);
        Check(failed.Actions.Count == 1, "failed send processed action twice");

        var crossing = new Rig(WithLiveBudget(NpcCatalog.All[0], 5, 0)) { NodeCost = 715 }; // 7 nodes: 5005 ms, the last crosses 5 s
        crossing.Runner.Tick(true); crossing.Runner.Tick(true);
        Check(crossing.Fallbacks.Count == 0, "clock cut replaced result completed in crossing slice");
        crossing.Runner.Tick(true);
        Check(crossing.Actions.Count == 1, "crossing result not acted");

        var failedFallback = new Rig(WithLiveBudget(NpcCatalog.All[0], 5, 0)) { NodeCost = 6000, ThrowFallback = true };
        failedFallback.Runner.Tick(true);
        try { failedFallback.Runner.Tick(true); } catch (IOException) { }
        Check(!failedFallback.Runner.Active, "throwing fallback left runner retrying");
        Check(failedFallback.Fallbacks.Count == 1, "throwing clock fallback called twice");

        var resumed = new Rig(WithLiveBudget(NpcCatalog.All[0], 5, 0));
        resumed.Runner.Tick(true); resumed.Runner.Tick();
        double search = resumed.Runner.SearchMilliseconds, delay = resumed.Runner.ArtificialDelayMilliseconds;
        resumed.Runner.Cancel(); resumed.Runner.Resume();
        Check(resumed.Runner.State == NpcTurnState.Thinking, "cancelled turn cannot resume");
        Check(resumed.Runner.SearchMilliseconds == search && resumed.Runner.ArtificialDelayMilliseconds == delay,
            "resume reset turn totals");
        var budget = new Rig(NpcCatalog.All[0], turnNodes: 7);
        budget.Runner.Tick(true); budget.Runner.Tick();
        budget.Runner.Cancel(); budget.Runner.Resume(); budget.Runner.Tick(true);
        Check(budget.Steps == 7 && budget.Fallbacks.SequenceEqual(new[] { "turn-nodes" }), "resume refunded spent nodes");
        var recovery = new Rig(NpcCatalog.All[0], turnNodes: 20) { ThrowSearch = true };
        recovery.Runner.Tick(true); recovery.Runner.Tick(true); recovery.Runner.Tick(true);
        recovery.Runner.Tick(true); recovery.Runner.Tick(true); recovery.Runner.Tick(true);
        Check(recovery.Fallbacks.Count == 1 && recovery.Fallbacks[0].Contains("injected-search") && recovery.Steps >= 8
            && recovery.Runner.TurnSearchNodes >= 8, "search failure preserves remaining node budget and charges failed step");
        var counted = new Rig(NpcCatalog.All[0], turnNodes: 100) { ThrowWrapped = true, SpendFallback = true };
        counted.Runner.Tick(true); counted.Runner.Tick(true); counted.Runner.Tick(true);
        Check(counted.Fallbacks.Single() == "InvalidOperationException:injected-original"
            && counted.Log.Any(line => line.Contains("nodes=2 ")) && counted.Runner.TurnSearchNodes == 2,
            "fallback preserves original error and reports failed and reserved nodes");
    }

    private static object Invoke(NativeMatch match, string name, params object[] args) =>
        typeof(NativeMatch).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(match, args);

    private static void HostFailuresAndChoices()
    {
        var previous = NpcPilots.Factory;
        try
        {
            NpcPilots.Factory = null;
            var secret = new NativeRig(NpcCatalog.All[0].DeckId, false);
            SabberStoneCore.Model.Entities.Entity.FromCard(secret.Match.Game.Player1, Cards.FromId("EX1_287"),
                zone: secret.Match.Game.Player1.SecretZone);
            secret.EndHumanTurn();
            Check(secret.Record.Decisions > 0 && secret.Match.BotFallbacks == 0, "human Secret forced live golden fallback");

            var invalid = new NativeRig(NpcCatalog.All[0].DeckId, false);
            invalid.EndHumanTurn(false);
            var illegal = new GoldenDecision(BotAction.From(ConcedeTask.Any(invalid.Match.Game.Player1)), 0, 0, 0, "Forced", null, null);
            int hashes = invalid.Record.Hashes.Count;
            Invoke(invalid.Match, "ActNpc", illegal);
            Check(invalid.Match.BotFallbacks == 1 && invalid.Record.Hashes.Count == hashes + 1,
                "unresolvable action did not process exactly one fallback");

            NpcPilots.Factory = p => new Pilot();
            var failed = new NativeRig(NpcCatalog.All[0].DeckId, false);
            failed.EndHumanTurn(false); failed.FailHistory = true;
            hashes = failed.Record.Hashes.Count;
            bool threw = false;
            for (int i = 0; i < 100 && !threw; i++)
            {
                failed.Now += 1000;
                try { failed.Match.TickNpc(); } catch (IOException) { threw = true; failed.Match.StopNpc(); }
            }
            for (int i = 0; i < 5; i++) failed.Match.TickNpc();
            Check(threw && failed.Record.Hashes.Count == hashes + 1 && failed.Record.Decisions == 1,
                "history failure replayed authoritative action");

            var interrupted = new NativeRig(NpcCatalog.All[0].DeckId, false, clockStep: .5);
            interrupted.EndHumanTurn(false);
            interrupted.Match.NpcRunner.SliceMilliseconds = 1; interrupted.Match.NpcRunner.IdleSliceMilliseconds = 1; interrupted.Match.NpcRunner.MaxIdleSliceMilliseconds = 1;
            interrupted.Now += 2000; interrupted.Match.TickNpc(); interrupted.Match.TickNpc();
            var runner = interrupted.Match.NpcRunner;
            double search = runner.SearchMilliseconds, delay = runner.ArtificialDelayMilliseconds;
            var human = interrupted.Match.Game.Player1;
            var offer = SabberStoneCore.Model.Entities.Entity.FromCard(human, Cards.FromId("CS2_231"), zone: human.SetasideZone);
            human.Choice = new Choice(human) { ChoiceType = ChoiceType.GENERAL, ChoiceAction = ChoiceAction.HAND,
                Choices = new List<int> { offer.Id }, SourceId = human.Hero.Id };
            interrupted.Match.TickNpc();
            Check(!runner.Active && interrupted.choices != null, "human choice did not interrupt NPC");
            interrupted.Match.SendChoices(interrupted.choices.Id, new List<int> { offer.Id });
            Check(ReferenceEquals(runner, interrupted.Match.NpcRunner) && runner.State == NpcTurnState.Thinking
                && runner.SearchMilliseconds == search && runner.ArtificialDelayMilliseconds == delay, "human answer reset turn budgets");
            runner.Cancel(); Invoke(interrupted.Match, "DriveUntilHumanInput");
            Check(ReferenceEquals(runner, interrupted.Match.NpcRunner) && runner.State == NpcTurnState.Thinking
                && runner.ArtificialDelayMilliseconds == delay, "cancelled native runner restarted pre-turn pause");

            NpcPilots.Factory = p => new UnslicedPilot();
            var legacy = new NativeRig(NpcCatalog.All[0].DeckId, false);
            legacy.EndHumanTurn();
            Check(legacy.Match.NpcRunner == null && legacy.Log.Count(x => x.Contains("not frame-sliced")) == 1
                && legacy.Match.Game.CurrentPlayer == legacy.Match.Game.Player1, "non-sliced pilot not routed to legacy search");

            NpcPilots.Factory = null;
            foreach (string cardId in new[] { "EDR_856", "TIME_034" })
            {
                var choice = new NativeRig(NpcCatalog.All[0].DeckId, false);
                choice.EndHumanTurn(false);
                var game = choice.Match.Game; var bot = game.Player2;
                bot.BaseMana = 10; bot.UsedMana = 0;
                var card = SabberStoneCore.Actions.Generic.DrawCard(bot, Cards.FromId(cardId));
                Invoke(choice.Match, "Process", bot.Options().First(t => t.Source?.Id == card.Id));
                Check(bot.Choice != null, "choice fixture missing " + cardId);
                bool rewind = cardId == "TIME_034";
                BotAction expected = null;
                if (rewind)
                {
                    bot.Hero.AddWeapon((SabberStoneCore.Model.Entities.Weapon)SabberStoneCore.Model.Entities.Entity.FromCard(bot, Cards.FromId("BE_227")));
                    game.Player1.Hero.AddWeapon((SabberStoneCore.Model.Entities.Weapon)SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, Cards.FromId("CORE_RLK_086")));
                    expected = BotAction.From(ChooseTask.Pick(bot, game.Rewind.RewindEntityId));
                }
                for (int i = 0; choice.Record.Decisions == 0 && i < 1000; i++) { choice.Now += 100; choice.Match.TickNpc(); }
                Check(choice.Record.Decisions == 1 && choice.Match.BotFallbacks == 0, "NPC choice fell back " + cardId);
                if (rewind) Check(choice.Record.LastDecision.Action.Equals(expected), "live Rewind ignored pilot hook");
            }

            NpcPilots.Factory = p => new Pilot();
            var budget = new NativeRig(NpcCatalog.All[0].DeckId, false, turnNodes: 1);
            budget.EndHumanTurn();
            Check(budget.Log.Any(x => x.Contains("fallback=turn-nodes")), "playtest node override not consumed");
        }
        finally { NpcPilots.Factory = previous; }
    }

    private sealed class UnslicedPilot : IGoldenPilot
    {
        public GoldenDecision Decide(FairBotState view, GoldenContext context) { throw new Exception("unsliced pilot called"); }
        public IReadOnlyList<int> Mulligan(IGoldenMulliganView view, GoldenContext context) => new int[0];
    }

    private static void RealGoldenMatches()
    {
        var previous = NpcPilots.Factory;
        try
        {
            NpcPilots.Factory = null;
            foreach (var profile in NpcCatalog.All)
            {
                Check(NpcPilots.Create(profile) is GoldenPilot, "default catalog pilot is not GoldenPilot");
                var sync = new NativeRig(profile.DeckId, true);
                var sliced = new NativeRig(profile.DeckId, false, clockStep: .02);
                for (int turn = 0; turn < 90 && sync.Match.Game.State != State.COMPLETE; turn++) sync.EndHumanTurn();
                for (int turn = 0; turn < 90 && sliced.Match.Game.State != State.COMPLETE; turn++) sliced.EndHumanTurn();
                Check(sync.Match.Game.State == State.COMPLETE && sliced.Match.Game.State == State.COMPLETE,
                    profile.Id + " real golden match unfinished");
                Check(sync.Record.Actions.Count > 0 && sync.Record.Actions.SequenceEqual(sliced.Record.Actions),
                    profile.Id + " real golden sync/sliced task sequence differs");
                Check(sync.Record.Hashes.SequenceEqual(sliced.Record.Hashes), profile.Id + " real golden fingerprints differ");
                Check(!sliced.Log.Any(x => x.Contains("clock-cut") || x.Contains("unregistered")), "real golden fell back");
            }
            var cancelled = new NativeRig(NpcCatalog.All[0].DeckId, false, clockStep: .5);
            cancelled.EndHumanTurn(false);
            cancelled.Match.NpcRunner.SliceMilliseconds = 1; cancelled.Match.NpcRunner.IdleSliceMilliseconds = 1; cancelled.Match.NpcRunner.MaxIdleSliceMilliseconds = 1;
            cancelled.Now += 2000; cancelled.Match.TickNpc(); cancelled.Match.TickNpc();
            Check(cancelled.Match.NpcRunner.State == NpcTurnState.Thinking, "real job did not yield mid-search");
            int decisions = cancelled.Record.Decisions, packets = cancelled.History.Count;
            cancelled.Match.StopNpc();
            for (int i = 0; i < 10; i++) cancelled.Match.TickNpc();
            Check(cancelled.Match.NpcRunner.State == NpcTurnState.Cancelled
                && cancelled.Record.Decisions == decisions && cancelled.History.Count == packets, "cancelled real job acted");
        }
        finally { NpcPilots.Factory = previous; }
    }

    private static void NativePaths()
    {
        var previous = NpcPilots.Factory;
        try
        {
            NpcPilots.Factory = p => new Pilot();
            foreach (var profile in NpcCatalog.All)
            {
                var sync = new NativeRig(profile.DeckId, true);
                var sliced = new NativeRig(profile.DeckId, false);
                for (int turn = 0; turn < 3; turn++) { sync.EndHumanTurn(); sliced.EndHumanTurn(); }
                Check(sync.Record.Actions.SequenceEqual(sliced.Record.Actions), "decisions differ before history");
                Check(sync.Record.Hashes.SequenceEqual(sliced.Record.Hashes), "hashes differ before history");
                Check(sync.History.SequenceEqual(sliced.History), profile.Id + " native history differs");
                Check(sync.Record.Hashes.SequenceEqual(sliced.Record.Hashes), "fingerprints differ");
                Check(sync.Record.Actions.SequenceEqual(sliced.Record.Actions), "decisions differ");
                Check(sync.Record.Hashes.Count == sync.Record.HumanActions + sync.Record.Decisions + 2,
                    "not exactly one fingerprint per action (including bot mulligan and MainBegin)");
                Check(sliced.Match.NpcRunner != null && sliced.Match.Game.CurrentPlayer == sliced.Match.Game.Player1, "NPC did not return input");
                Check(sliced.Log.Any(x => x.Contains("npc=") && x.Contains("reuse=") && x.Contains("lethal=") && x.Contains("slice=")), "log fields missing");
            }
            NativeRig concede = null;
            NpcPilots.Factory = p => new Pilot(() => concede.Now += 3);
            concede = new NativeRig(NpcCatalog.All[0].DeckId, false);
            concede.EndHumanTurn(false);
            concede.Match.NpcRunner.IdleSliceMilliseconds = concede.Match.NpcRunner.MaxIdleSliceMilliseconds = 10;
            concede.Now += 2000; concede.Match.TickNpc(); concede.Match.TickNpc();
            Check(concede.Match.NpcRunner.State == NpcTurnState.Thinking, "fixture not thinking");
            concede.Match.Concede();
            int packets = concede.History.Count;
            for (int i = 0; i < 20; i++) concede.Match.TickNpc();
            Check(concede.Match.NpcRunner.State == NpcTurnState.Cancelled && concede.History.Count == packets,
                "concession left stale job/history");
            Check(concede.Record.Results == 1 && concede.Record.Concessions == 1, "concession/result repeated");
            string folder = Path.Combine(Path.GetTempPath(), "npc-settlement-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string path = Path.Combine(folder, "profile.xml");
                using (var profile = new LocalProfile(path))
                {
                    var pending = profile.Begin("mage", NpcCatalog.All[0].DeckId, 8427);
                    profile.Settle(pending.Id, "Lost", concede.Match.Game.Turn, true, concede.Match.RewardFacts);
                    var bytes = File.ReadAllBytes(path);
                    profile.Settle(pending.Id, "Lost", concede.Match.Game.Turn, true, concede.Match.RewardFacts);
                    Check(bytes.SequenceEqual(File.ReadAllBytes(path)), "concession settled twice");
                }
            }
            finally { Directory.Delete(folder, true); }
            NpcPilots.Factory = p => new Pilot();
            var complete = new NativeRig(NpcCatalog.All[0].DeckId, false);
            complete.EndHumanTurn(false);
            complete.Match.Game.Process(ConcedeTask.Any(complete.Match.Game.Player2));
            complete.Match.TickNpc();
            Check(!complete.Match.NpcRunner.Active && complete.Record.Decisions == 0, "completion did not stop work");
            var stop = new NativeRig(NpcCatalog.All[0].DeckId, false);
            stop.EndHumanTurn(false); stop.Match.StopNpc(); stop.Match.TickNpc();
            Check(stop.Match.NpcRunner.State == NpcTurnState.Cancelled && stop.Record.Decisions == 0, "stop did not cancel");
            var timer = new NativeRig(NpcCatalog.All[0].DeckId, false);
            timer.Match.Game.Player2[GameTag.TIMEOUT] = 15;
            timer.EndHumanTurn(false);
            timer.Match.TickTurnTimer(long.MaxValue);
            Check(timer.Match.Game.CurrentPlayer == timer.Match.Game.Player1 && !timer.Match.NpcRunner.Active, "timer failed to end NPC turn");
            Check(timer.Record.Decisions == 1 && timer.Record.Hashes.Count == timer.Record.HumanActions + timer.Record.Decisions + 2,
                "timeout action missing recorder decision/fingerprint");
            var legacy = new NativeRig("mage", false); var legacySync = new NativeRig("mage", true);
            legacy.EndHumanTurn(); legacySync.EndHumanTurn();
            Check(legacy.Match.NpcRunner == null && legacySync.Match.NpcRunner == null, "legacy created runner");
            // Historical legacy behavior is pinned by ShowcasePlayProof's table, checked before/after this change.
            int hashes = 0;
            var disabled = new NativeRig(NpcCatalog.All[0].DeckId, true, false);
            disabled.Match.ReplayFingerprint = g => { hashes++; throw new Exception("disabled hash invoked"); };
            disabled.EndHumanTurn();
            Check(hashes == 0, "null recorder computed hash");
            var failing = new NativeRig(NpcCatalog.All[0].DeckId, true);
            failing.Match.ReplayFingerprint = g => { throw new IOException("full disk"); };
            failing.EndHumanTurn();
            Check(failing.Match.Game.CurrentPlayer == failing.Match.Game.Player1, "recorder failure affected match");
            NpcPilots.Factory = null;
            Check(NpcPilots.Create(NpcCatalog.All[0]) is IFrameSlicedGoldenPilot, "missing registered pilot fallback");
        }
        finally { NpcPilots.Factory = previous; }
    }

    private sealed class Pilot : IFrameSlicedGoldenPilot
    {
        private readonly Action step;
        internal Pilot(Action step = null) { this.step = step; }
        public IGoldenDecisionJob Begin(FairBotState view, GoldenContext context) => new PilotJob(view, step);
        public GoldenDecision Decide(FairBotState view, GoldenContext context)
        { var job = Begin(view, context); while (!job.IsDone) job.StepNode(); return job.Result; }
        public IReadOnlyList<int> Mulligan(IGoldenMulliganView view, GoldenContext context) => new int[0];
    }
    private sealed class PilotJob : IGoldenDecisionJob
    {
        private readonly FairBotState view;
        private int nodes;
        private readonly Action step;
        internal PilotJob(FairBotState view, Action step) { this.view = view; this.step = step; }
        public bool IsDone => nodes == 7;
        public void StepNode() { nodes++; step?.Invoke(); }
        public GoldenDecision Result => new GoldenDecision(view.OrderedActions[0], 1, nodes, 1, "Nodes", null, null);
        public string LethalResult => "none";
        public bool PlanReused => false;
    }
    private sealed class Recording : IMatchRecorder
    {
        public bool Enabled => true;
        internal readonly List<string> Hashes = new List<string>(), Actions = new List<string>();
        internal int HumanActions, Decisions, Results, Concessions;
        internal GoldenDecision LastDecision;
        public void OnMulligan(int playerId, IReadOnlyList<int> kept) { }
        public void OnDecision(GoldenContext context, GoldenDecision trace, string lethal, bool reuse, int nodes, double ms, double slice)
        { Decisions++; LastDecision = trace; Actions.Add(trace.Action.ToString()); }
        public void OnHumanAction(int turn, string action, int index, IReadOnlyList<int> choices, bool conceded) { HumanActions++; }
        public void OnEmote(int playerId, string emote, int eventIndex) { }
        public void OnConcession(int playerId, int turn) { Concessions++; }
        public void OnResult(string result, int turns) { Results++; }
        public void OnPostAction(long sequence, string hash)
        { Check(sequence == Hashes.Count + 1, "fingerprint sequence gap"); Hashes.Add(hash); }
    }
    private sealed class NativeRig
    {
        internal double Now;
        internal readonly NativeMatch Match;
        internal readonly Recording Record = new Recording();
        internal readonly List<string> History = new List<string>(), Log = new List<string>();
        internal readonly List<KeyValuePair<double, UserUI>> UserUi = new List<KeyValuePair<double, UserUI>>();
        internal EntityChoices choices;
        internal bool FailHistory;
        private AllOptions options;
        internal NativeRig(string deck, bool synchronous, bool record = true, double clockStep = 0, int? turnNodes = null, bool presentation = false)
        {
            Match = new NativeMatch((id, packet) => {
                if (id == 19 && FailHistory) throw new IOException("history send failed");
                if (id == 17) choices = (EntityChoices)packet;
                if (id == 14) options = (AllOptions)packet;
                using (var bytes = new MemoryStream()) { packet.Serialize(bytes); if (id == 19) History.Add(Convert.ToBase64String(bytes.ToArray())); }
            }, "mage", deck, 8427) { NpcTurnNodes = turnNodes, SynchronousNpc = synchronous, MatchId = "fixed-match", NpcClock = () => { Now += clockStep; return Now; }, NativeNpcIdle = () => true };
            Match.PresentationSettings = new NpcPresentationSettings { Emotes = presentation, Hover = presentation,
                Skin = presentation, CardBack = presentation };
            Match.NativeNpcStartingTaunt = () => false;
            Match.NativeNpcUserUI = packet => { UserUi.Add(new KeyValuePair<double, UserUI>(Now, packet)); return true; };
            if (record) Match.Recorder = Record;
            Match.BotTrace = Log.Add;
            Match.Start(); Match.SendChoices(choices.Id, new List<int>());
        }
        internal void EndHumanTurn(bool pump = true)
        {
            int index = options.Options.FindIndex(o => o.Type_ == PegasusGame.Option.Type.END_TURN);
            Match.SendOption(options.Id, index, 0, -1, 0);
            if (!pump) return;
            for (int i = 0; Match.NpcRunner?.Active == true && i < 10000; i++) { Now += 100; Match.TickNpc(); }
            Check(Match.NpcRunner?.Active != true, "native runner stuck");
        }
    }

    // An endless free loop (Farseer Wo + a 0-cost Ancestral Healing) keeps choosing a non-ending action: the turn ends
    // at MaxTurnActions through the "action-limit" fallback (NativeMatch plays END_TURN), never at the 75 s deadline.
    private static void ActionLimitEndsTheTurn()
    {
        var profile = NpcCatalog.All[0];
        var game = new SabberStoneCore.Model.Game(new GameConfig { Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.MAGE });
        var action = BotAction.From(EndTurnTask.Any(game.Player2));
        int searched = 0; var fallbacks = new List<string>(); bool ended = false;
        var runner = new NpcTurnRunner(profile, "action-limit", () => 0, () => !ended && searched + fallbacks.Count < 4 * NpcTurnRunner.MaxTurnActions,
            () => new GoldenContext(2, searched + 1, 71, profile), ctx => new LoopJob(action),
            reason => { fallbacks.Add(reason); return new GoldenDecision(action, 0, 0, 0, "Forced", reason, null, "fallback"); },
            decision => { if (decision.FallbackReason == "action-limit") ended = true; else searched++; }, () => true);
        runner.Start(); runner.RunSynchronously();
        Check(ended && searched == NpcTurnRunner.MaxTurnActions && fallbacks.Count == 1 && fallbacks[0] == "action-limit",
            $"loop turn ends at the action limit: searched {searched}, fallbacks {string.Join(",", fallbacks)}");
    }
    private sealed class LoopJob : IGoldenDecisionJob
    {
        private readonly BotAction action;
        internal LoopJob(BotAction action) { this.action = action; }
        public bool IsDone { get; private set; }
        public GoldenDecision Result => new GoldenDecision(action, 1, 1, 1, "Nodes", null, null);
        public string LethalResult => "none";
        public bool PlanReused => false;
        public void StepNode() { IsDone = true; }
    }

    private sealed class Job : IGoldenDecisionJob
    {
        private readonly Rig rig;
        private int nodes;
        internal Job(Rig rig) { this.rig = rig; }
        public bool IsDone => nodes == 7;
        public GoldenDecision Result => new GoldenDecision(rig.Action, 1, nodes, 1, "Nodes", null, null);
        public string LethalResult => "none";
        public bool PlanReused => false;
        public void StepNode() { nodes++; rig.Steps++; rig.Now += rig.NodeCost; if (rig.ThrowSearch) { rig.ThrowSearch = false; throw new InvalidOperationException("injected-search"); } if (rig.ThrowWrapped) { rig.ThrowWrapped = false; throw new SimulationFailureException(rig.Action, new InvalidOperationException("injected-original")); } }
    }

    private sealed class Rig
    {
        internal double Now, NodeCost = 3;
        internal int Steps;
        internal bool Idle = true, ThrowAct, ThrowFallback, ThrowSearch, ThrowWrapped, SpendFallback;
        internal string Busy;
        internal readonly List<string> Actions = new List<string>(), Fallbacks = new List<string>(), Log = new List<string>();
        internal readonly NpcTurnRunner Runner;
        internal readonly BotAction Action;
        internal Rig(NpcProfile profile, int? turnNodes = null)
        {
            var game = new SabberStoneCore.Model.Game(new GameConfig { Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.MAGE });
            Action = BotAction.From(EndTurnTask.Any(game.Player2));
            Runner = new NpcTurnRunner(profile, "same-match", () => Now, () => Actions.Count < 30,
                () => new GoldenContext(2, Actions.Count + 1, 997 * 2 + (Actions.Count + 1) * 31, profile),
                ctx => new Job(this), reason => { Fallbacks.Add(reason); if (SpendFallback) Check(Runner.SpendFallbackNode(), "fallback reservation unavailable"); if (ThrowFallback) throw new IOException("fallback failed"); return new GoldenDecision(Action, 0, 0, 0, "Forced", reason, null, "fallback"); },
                decision => { Actions.Add(decision.Action.ToString()); if (ThrowAct) throw new IOException("send failed"); }, () => Idle, Log.Add, turnNodes: turnNodes,
                busyReason: () => Busy);
            Runner.Start();
            Check(Runner.ArtificialDelayMilliseconds >= profile.Personality.PreTurnMinSeconds * 1000
                && Runner.ArtificialDelayMilliseconds <= profile.Personality.PreTurnMaxSeconds * 1000, "wrong profile pacing");
        }
        internal void Run(bool sync, double frame = 10)
        {
            if (sync) Runner.RunSynchronously();
            else for (int i = 0; Runner.Active && i < 10000; i++) { Now += frame; Runner.Tick(); }
            Check(!Runner.Active, "runner did not finish");
        }
    }
}
