using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Tasks.PlayerTasks;

// Guards the capped-pass fix: a search stopped by the live clock or node cap left an unfinished line, the planner completed it
// with a synthetic END_TURN, and later decisions that turn reused the cached pass (the NPC ended its turn with a 5/9 Taunt
// still standing). Fixed by never completing an unfinished line with END_TURN and by validating a capped pass with an
// independent 7,000-node search (ValidationNodeCap). Two halves, two groups of cases:
//   - the validation half: ValidationLethal and friends (SolverClockLethal, WorkerLethalAtValidationBoundary, WrappedDeadline,
//     EliteSecret, Generic, PricingAtReplyLimitZero);
//   - the no-synthetic-END_TURN half: CappedLineNeverEndsWithSyntheticEndTurn.
// The recorded playtest game (a Mage deck against the Hunter "Magmaw" NPC deck, turn 22) that first showed the bug was removed
// on 2026-11-18 (owner ruling): the no-duplicate rule for Tricksy Improviser (#179) changes that game from its row 29, and a
// recording of a real human player's game with a clock-capped search cannot be re-recorded.
internal static class CappedPassSuite
{
    private static void SeedCache(GoldenPilot pilot, FairBotState view, BotAction power, BotAction end)
    {
        pilot.Accept(view, new[] { power, end }, 0, LethalOutcome.Incomplete);
        // Also compiles against the pre-fix pilot for the red/green check.
        typeof(GoldenPilot).GetProperty("CachedInterrupted", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(pilot, true);
    }
    private static GoldenDecision Cut(GoldenDecisionJob job)
    { job.StopSearch(SearchStop.Clock); return Finish(job); }
    private static GoldenDecision Finish(GoldenDecisionJob job)
    { while (!job.IsDone) job.StepNode(); return job.Result; }
    private static void Generic()
    {
        foreach (bool clock in new[] { false, true }) {
            var game = PassivitySuite.Fixture(2); RoundTwoSuite.Body(game, true, 3, 3); RoundTwoSuite.Body(game, false, 1, 1, true);
            var view = PassivitySuite.View(game); var profile = RoundTwoSuite.Profile(cap: 7000);
            var pilot = new GoldenPilot(profile, PassivitySuite.Plan()); pilot.Job(view, RoundTwoSuite.Context(view, profile));
            var power = view.Actions.Single(a => a.Type == PlayerTaskType.HERO_POWER);
            var end = view.Actions.Single(a => a.Type == PlayerTaskType.END_TURN);
            SeedCache(pilot, view, power, end);
            pilot.ChargePlan(int.MaxValue); var current = view.Apply(power);
            var job = pilot.Job(current, RoundTwoSuite.Context(current, profile, 2)); job.CapPlanNodes(() => 1);
            var decision = clock ? Cut(job) : Finish(job);
            Program.Require(decision.Action.Type == PlayerTaskType.MINION_ATTACK && decision.SearchInfo.ValidationNodes > 0,
                "capped cached END_TURN cannot bypass better continuation, clock=" + clock);
            var completed = current.Apply(decision.Action);
            var kept = pilot.Job(completed, RoundTwoSuite.Context(completed, profile, 3)).StopSearch(SearchStop.Clock);
            Program.Require(kept.Action.Type == PlayerTaskType.END_TURN && pilot.PlanReused
                && kept.SearchInfo.ValidationNodes == 0, "completed validation replaces incomplete cache provenance");
        }
        Console.WriteLine("PASS generic capped cached pass: ordinary and clock-exhausted primary budgets");
    }
    private static void ValidationLethal()
    {
        foreach (string mode in new[] { "clock", "reserve-only", "deadline", "decorated-deadline", "decorated-complete", "decorated-clock" }) {
            var game = PassivitySuite.Fixture(2); RoundTwoSuite.Body(game, true, 3, 3);
            game.Player2.Hero.Health = 3;
            var view = PassivitySuite.View(game); var profile = RoundTwoSuite.Profile(cap: 7000);
            var pilot = new GoldenPilot(profile, PassivitySuite.Plan()); pilot.Job(view, RoundTwoSuite.Context(view, profile));
            var power = view.Actions.Single(a => a.Type == PlayerTaskType.HERO_POWER);
            var end = view.Actions.Single(a => a.Type == PlayerTaskType.END_TURN);
            SeedCache(pilot, view, power, end); pilot.ChargePlan(int.MaxValue); view = view.Apply(power);
            var job = pilot.Job(view, RoundTwoSuite.Context(view, profile, 2));
            if (mode == "reserve-only") {
                while (!job.Validating && !job.IsDone) job.StepNode();
                Program.Require(job.Validating && job.LethalResult != "found", "lethal must be found only inside reserve");
            } else Program.Require(job.StopSearch(SearchStop.Clock) == null && job.Validating, "clock leaves reserve pending");
            GoldenDecision result;
            if (mode == "deadline" || mode.StartsWith("decorated-", StringComparison.Ordinal)) {
                var planner = (TurnPlannerJob)typeof(GoldenDecisionJob).GetField("validation",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(job);
                // Stop between the reserve finding its witness and the outer job publishing it.
                while (!planner.IsDone) planner.StepNode();
                Program.Require(planner.Result.Value == 1000000 && !planner.HasPassComparison,
                    "verified lethal completes before reply comparison");
                if (mode.StartsWith("decorated-", StringComparison.Ordinal)) {
                    var skill = SkillCatalog.Parse(Program.Data("skills-v1.json")).Find("master");
                    var wrapped = new SkilledPilot(pilot, null, skill, 1).Begin(view, RoundTwoSuite.Context(view, profile, 2));
                    var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                    wrapped.GetType().GetField("current", flags).SetValue(wrapped, job);
                    wrapped.GetType().GetField("baseline", flags).SetValue(wrapped,
                        new GoldenDecision(end, 99, 1, 1, "Exhausted", null, null));
                    wrapped.GetType().GetField("variantKind", flags).SetValue(wrapped, MistakeKind.IgnoreAoeRisk);
                    if (mode == "decorated-deadline") result = ((INpcPassValidation)wrapped).StopValidation();
                    else {
                        if (mode == "decorated-clock") Program.Require(((INpcSearchContinuation)wrapped).StopSearch(SearchStop.Clock) == null,
                            "decorated clock cut retains pending validation");
                        while (!wrapped.IsDone) wrapped.StepNode();
                        result = wrapped.Result;
                    }
                    Program.Require(result.Nodes == job.Result.Nodes + 1, "decorated lethal counts each search once: " + mode);
                } else result = job.StopValidation();
            } else result = Finish(job);
            Program.Require(result.Action.Type == PlayerTaskType.MINION_ATTACK
                && result.Action.Target == view.Facts.Opponent.Hero.EntityId && result.Value == 1000000,
                "validation cannot discard proven lethal: " + mode + " action=" + result.Action + " value=" + result.Value);
            Program.Require(!result.SearchInfo.ValidationCompared && !result.SearchInfo.ValidationFallback
                && result.SearchInfo.ValidationNodes > 0, "lethal needs no pass comparison or fallback: " + mode);
            Program.Require(result.SearchInfo.ValidationDeadline == mode.EndsWith("deadline", StringComparison.Ordinal),
                "lethal retains deadline flag: " + mode);
            Program.Require(view.Apply(result.Action).IsTerminal, "published lethal executes on real fair engine: " + mode);
            Console.WriteLine("PASS validation lethal " + mode + ": " + result.Action + " nodes=" + result.SearchInfo.ValidationNodes);
        }
    }
    private static void SolverClockLethal()
    {
        foreach (bool decorated in new[] { false, true }) {
            var game = PassivitySuite.Fixture(0); RoundTwoSuite.Body(game, true, 3, 3); game.Player2.Hero.Health = 3;
            var view = PassivitySuite.View(game); var profile = NpcCatalog.All[0]; var pilot = new GoldenPilot(profile, PassivitySuite.Plan());
            var context = RoundTwoSuite.Context(view, profile); var job = pilot.Job(view, context);
            while (!job.LethalSearch.IsDone) job.LethalSearch.StepNode();
            Program.Require(job.LethalResult == "found" && !job.IsDone, "solver proves lethal before clock publication");
            IGoldenDecisionJob current = job;
            if (decorated) {
                var skill = SkillCatalog.Parse(Program.Data("skills-v1.json")).Find("master");
                current = new SkilledPilot(pilot, null, skill, 1).Begin(view, context);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                current.GetType().GetField("current", flags).SetValue(current, job);
                current.GetType().GetField("baseline", flags).SetValue(current,
                    new GoldenDecision(view.Actions.Single(a => a.Type == PlayerTaskType.END_TURN), 99, 1, 1, "Exhausted", null, null));
                current.GetType().GetField("variantKind", flags).SetValue(current, MistakeKind.IgnoreAoeRisk);
            }
            var result = ((INpcSearchContinuation)current).StopSearch(SearchStop.Clock);
            Program.Require(result.Action.Type == PlayerTaskType.MINION_ATTACK && result.Value == 1000000
                && view.Apply(result.Action).IsTerminal && !result.SearchInfo.ValidationFallback,
                "clock publication must retain solver lethal score and line, decorated=" + decorated);
        }
        Console.WriteLine("PASS solver lethal survives direct and decorated clock publication");
    }
    private static void WorkerLethalAtValidationBoundary()
    {
        bool previous = GoldenPilot.ParallelLethalEnabled;
        GoldenPilot.ParallelLethalEnabled = true;
        GoldenDecisionJob job = null;
        try {
            var game = PassivitySuite.Fixture(0); RoundTwoSuite.Body(game, true, 3, 3); game.Player2.Hero.Health = 3;
            var view = PassivitySuite.View(game); var profile = NpcCatalog.All[0];
            var pilot = new GoldenPilot(profile, PassivitySuite.Plan()); job = pilot.Job(view, RoundTwoSuite.Context(view, profile));
            job.PollBackground();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var parallel = typeof(GoldenDecisionJob).GetField("parallel", flags).GetValue(job);
            var workers = (System.Threading.Tasks.Task[])parallel.GetType().GetField("running", flags).GetValue(parallel);
            System.Threading.Tasks.Task.WaitAll(workers);
            Program.Require(job.LethalResult != "found", "worker witness remains unpublished before owner collection");
            // Freeze the boundary after the guard reads progress but before Stop collects the completed workers.
            typeof(GoldenDecisionJob).GetMethod("Complete", flags).Invoke(job, new object[] {
                new[] { view.Actions.Single(a => a.Type == PlayerTaskType.END_TURN) }, 0d, SearchStop.Nodes, null });
            var result = job.IsDone ? job.Result : job.StopValidation();
            Program.Require(job.LethalResult == "found" && result.Value == 1000000
                && result.Action.Type == PlayerTaskType.MINION_ATTACK && view.Apply(result.Action).IsTerminal
                && !result.SearchInfo.ValidationFallback,
                "worker lethal collected at validation entry must replace pass");
            Console.WriteLine("PASS worker lethal collected at validation boundary replaces pass");
        }
        finally { job?.StopBackground(); GoldenPilot.ParallelLethalEnabled = previous; }
    }
    private static void EliteSecret()
    {
        var game = PassivitySuite.Fixture(2);
        RoundTwoSuite.Body(game, true, 3, 3); RoundTwoSuite.Body(game, false, 1, 1, true);
        SabberStoneCore.Model.Entities.Entity.FromCard(game.Player1, Cards.FromId("EX1_287"), zone: game.Player1.SecretZone);
        var skill = SkillCatalog.Parse(Program.Data("skills-v1.json")).Find("elite");
        var p = NpcCatalog.All[0];
        var profile = new NpcProfile(p.Id, p.Version, p.DisplayName, p.DeckId, p.HeroSkin, p.CardBack,
            p.Personality, skill.Budget, p.PlanId, p.HeroSkinPower);
        var view = PassivitySuite.View(game); var plan = PassivitySuite.Plan();
        var normal = RoundTwoSuite.Search(view, plan, profile);
        Program.Require(normal.Line.Any(a => a.Type == PlayerTaskType.MINION_ATTACK), "elite Secret normal continuation");
        var validation = TurnPlanner.Job(view, profile, plan, 71, 7000, comparePass: true);
        while (!validation.IsDone) validation.StepNode();
        Console.WriteLine("elite Secret comparison=" + validation.HasPassComparison + " nodes=" + validation.Result.Nodes
            + " candidates=" + string.Join(";", validation.Result.Candidates.Select(c => c.Key + "=" + c.Value))
            + " line=" + string.Join(";", validation.Result.Line));
        Program.Require(validation.HasPassComparison && validation.Result.Line.Any(a => a.Type == PlayerTaskType.MINION_ATTACK),
            "elite Counterspell must compare pass and real continuation with full replies");
        Program.Require(validation.Result.Candidates.Any(c => c.Key.Type == PlayerTaskType.END_TURN)
            && validation.Result.Candidates.Any(c => c.Key.Type != PlayerTaskType.END_TURN), "both completed reply scores retained");
        var pilot = new GoldenPilot(profile, plan); pilot.Job(view, RoundTwoSuite.Context(view, profile));
        var power = view.Actions.Single(a => a.Type == PlayerTaskType.HERO_POWER);
        var end = view.Actions.Single(a => a.Type == PlayerTaskType.END_TURN);
        SeedCache(pilot, view, power, end); pilot.ChargePlan(int.MaxValue);
        var current = view.Apply(power);
        var result = Cut(pilot.Job(current, RoundTwoSuite.Context(current, profile, 2)));
        Program.Require(result.Action.Type == PlayerTaskType.MINION_ATTACK && result.SearchInfo.ValidationCompared
            && !result.SearchInfo.ValidationFallback, "elite Secret cached pass cannot certify itself");
        Console.WriteLine("PASS elite Counterspell 3/3 vs 1/1 Taunt: direct and cached pass comparisons");
    }
    private static void WrappedDeadline()
    {
        var game = PassivitySuite.Fixture(2);
        RoundTwoSuite.Body(game, true, 3, 3); RoundTwoSuite.Body(game, false, 1, 1, true);
        var view = PassivitySuite.View(game); var profile = RoundTwoSuite.Profile(cap: 7000);
        var primary = new GoldenPilot(profile, PassivitySuite.Plan()); primary.Job(view, RoundTwoSuite.Context(view, profile));
        var power = view.Actions.Single(a => a.Type == PlayerTaskType.HERO_POWER);
        var end = view.Actions.Single(a => a.Type == PlayerTaskType.END_TURN);
        SeedCache(primary, view, power, end); primary.ChargePlan(int.MaxValue); view = view.Apply(power);
        var skill = SkillCatalog.Parse(Program.Data("skills-v1.json")).Find("master");
        var pilot = new SkilledPilot(primary, null, skill, 1);
        var inner = pilot.Begin(view, RoundTwoSuite.Context(view, profile, 2));
        Program.Require(((INpcSearchContinuation)inner).StopSearch(SearchStop.Clock) == null,
            "decorated pass validation remains pending");
        var baseline = new GoldenDecision(view.Actions.First(a => a.Type == PlayerTaskType.MINION_ATTACK), 99, 1, 1, "Exhausted", null, null);
        // Model a variant's pending reserve after the original search already supplied a baseline.
        inner.GetType().GetField("baseline", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(inner, baseline);
        var result = ((INpcPassValidation)inner).StopValidation();
        Program.Require(result.Action.Equals(baseline.Action) && result.SearchInfo.ValidationDeadline
            && result.SearchInfo.ValidationFallback, "deadline cannot accept an unpriced mistake variant");
        Console.WriteLine("PASS decorated validation deadline retains original baseline before mistake pricing");
    }
    // Review round 3: mistake pricing certifies a continuation only when it ends the turn (SkilledPilot.Complete). Below
    // ~1,960 plan nodes it has no reply finalists, and its best complete line is a released own-beam leaf: Whirlwinds keep
    // the beam two depths past Wisp/Wisp/Armor Up. That leaf must keep END_TURN, or every priced mistake is rejected.
    private static void PricingAtReplyLimitZero()
    {
        var game = PassivitySuite.Fixture(10, "CS2_231", "CS2_231", "EX1_400", "EX1_400");
        int wisp = game.Player1.HandZone[0].Id;
        var view = PassivitySuite.View(game); var profile = NpcCatalog.All[0];
        Program.Require(profile.Fixes.On("attack-allocation"), "default profile runs attack-allocation");
        var skill = SkillCatalog.Parse(Program.Data("skills-v1.json")).Find("master");
        var owner = new SkilledPilot(new GoldenPilot(profile, PassivitySuite.Plan()), null, skill, 1);
        var job = owner.Begin(view, RoundTwoSuite.Context(view, profile));
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var proposed = view.Actions.First(a => a.Type == PlayerTaskType.PLAY_CARD && a.Source == wisp);
        var raw = new GoldenDecision(proposed, -1000, 1, 1, "Exhausted", null, null);
        job.GetType().GetField("raw", flags).SetValue(job, raw);
        job.GetType().GetField("choice", flags).SetValue(job, new SkilledPilot.Choice { Index = 1 });
        job.GetType().GetField("pricingCap", flags).SetValue(job, 1500);
        job.GetType().GetMethod("StartPrice", flags).Invoke(job, new object[] { proposed });
        var pricing = (TurnPlannerJob)job.GetType().GetField("pricing", flags).GetValue(job);
        Program.Require(pricing != null && (int)typeof(TurnPlannerJob).GetField("replyLimit", flags).GetValue(pricing) == 0,
            "pricing at a late live cap has no reply finalists");
        var beam = (TurnBeam)typeof(TurnPlannerJob).GetField("beam", flags).GetValue(pricing);
        while (!beam.IsDone) job.StepNode();
        var best = TurnLine.Rank((System.Collections.Generic.IEnumerable<TurnLine>)typeof(TurnPlannerJob)
            .GetMethod("Candidates", flags).Invoke(pricing, null)).First();
        Program.Require(best.Completed && !best.Holds, "fixture: priced best complete leaf released its clone");
        while (!job.IsDone) job.StepNode();
        var reference = (PlannedTurn)job.GetType().GetField("reference", flags).GetValue(job);
        Program.Require(reference != null && reference.Line.Last().Type == PlayerTaskType.END_TURN,
            "released priced line keeps END_TURN: " + string.Join(";", reference?.Line ?? new BotAction[0]));
        Program.Require(job.Result != raw && job.Result.SearchInfo.Mistake == "softmax" && owner.SoftmaxDeviations == 1,
            "mistake pricing at replyLimit 0 completes and is accepted");
        Console.WriteLine("PASS mistake pricing at replyLimit 0 keeps END_TURN on a released best: " + string.Join(";", reference.Line));
    }
    // A search cut short by its node cap must not finish its line with a synthetic END_TURN: the line is a prefix and the turn goes on.
    private static void CappedLineNeverEndsWithSyntheticEndTurn()
    {
        int capped = 0;
        foreach (bool allocation in new[] { true, false }) {
            
            for (int cap = 3; cap <= 400; cap += 7) {
                var game = PassivitySuite.Fixture(10, "CS2_182", "CS2_182");
                RoundTwoSuite.Body(game, true, 3, 3); RoundTwoSuite.Body(game, true, 2, 4); RoundTwoSuite.Body(game, true, 4, 2);
                RoundTwoSuite.Body(game, false, 2, 2, true); RoundTwoSuite.Body(game, false, 3, 1); RoundTwoSuite.Body(game, false, 1, 5);
                var run = RoundTwoSuite.Profile(cap: cap);
                if (!allocation) run = run.WithFixes(new AiFixes(new[] { "starved-replies", "attack-allocation" }));
                var job = TurnPlanner.Job(PassivitySuite.View(game), run, PassivitySuite.Plan(), 71, cap);
                while (!job.IsDone) job.StepNode();
                var result = job.Result;
                if (!result.Interrupted || result.Line.Count == 0 || result.Line[0].Type == PlayerTaskType.END_TURN) continue;
                capped++;
                Program.Require(result.Line.Last().Type != PlayerTaskType.END_TURN,
                    "a capped line is not completed with a synthetic END_TURN (cap " + cap + ", allocation " + allocation + "): " + string.Join(";", result.Line));
            }
        }
        Program.Require(capped > 20, "the sweep reaches capped lines: " + capped);
        Console.WriteLine("PASS capped lines are never completed with a synthetic END_TURN: " + capped + " capped lines");
    }
    internal static void Run() { CappedLineNeverEndsWithSyntheticEndTurn(); PricingAtReplyLimitZero(); WorkerLethalAtValidationBoundary(); SolverClockLethal(); ValidationLethal(); WrappedDeadline(); EliteSecret(); Generic(); }
}
