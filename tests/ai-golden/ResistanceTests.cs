using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

public static class ResistanceTests
{
    public static void TestLossRankingKeepsExactPriorities()
    {
        var plain = new TurnLine { Value = -998000 };
        var loss = new TurnLine { Value = -999500, Survival = .99, Resistance = 10000, FaceDamage = 100 };
        Check(TurnLine.Rank(new[] { loss, plain }).First() == plain, "terminal risk outranks safe line");
        var survival = new TurnLine { Survival = .99000001, Resistance = -10000 };
        Check(TurnLine.Rank(new[] { loss, survival }).First() == survival, "resistance overrides survival");
        var board = new TurnLine { Survival = .99, Resistance = 10001, FaceDamage = -100 };
        Check(TurnLine.Rank(new[] { loss, board }).First() == board, "face damage overrides resistance");
        var face = new TurnLine { Survival = .99, Resistance = 10000, FaceDamage = 101, UnusedResources = 100 };
        Check(TurnLine.Rank(new[] { loss, face }).First() == face, "waste overrides face damage");
        var spent = new TurnLine { Survival = .99, Resistance = 10000, FaceDamage = 100, UnusedResources = -1 };
        Check(TurnLine.Rank(new[] { loss, spent }).First() == spent, "unused resources ignored");
    }
    public static void TestTerminalRepliesRetainSurvivalAndResistance()
    {
        var game = Fixture(); game.Player1.Hero.Damage = 29;
        Board(game, false, "CS2_182", true);
        var evaluator = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
        var state = View(game); var job = ReplyModel.Job(state, NpcCatalog.All[0], 71, evaluator); Run(job);
        Check(job.Survival == 0, "all sampled hands must win against one health");
        Board(game, true, "CS2_182");
        Check(evaluator.Resistance(View(game).Facts) > evaluator.Resistance(state.Facts), "own surviving body has no resistance value");
        var outcomes = new[] { new System.Collections.Generic.KeyValuePair<double, double>(0, .25),
            new System.Collections.Generic.KeyValuePair<double, double>(1, .75) };
        Check(ReplyModel.Blend(outcomes, .55) < ReplyModel.Blend(outcomes, .15), "beta no longer weights survival risk");
    }
    public static void TestLethalTurnBudgetIncludesAbandonedJobsAndResets()
    {
        var profile = NpcCatalog.All[0]; var pilot = new GoldenPilot(profile);
        var game = Fixture();
        for (int i = 0; i < 7; i++) Board(game, true, "CS2_182", true);
        game.Player2.Hero.Armor = 100;
        for (int i = 0; i < 3; i++) {
            var job = pilot.Job(View(game), new GoldenContext(game.Turn, i, 71, profile));
            var lethal = (LethalJob)job.LethalSearch;
            Check(lethal.Cap == profile.Budget.LethalNodes, "full per-decision allowance missing");
            Run(lethal);
            Check(lethal.Result.Nodes == profile.Budget.LethalNodes, "fixture did not exhaust lethal allowance");
        }
        var exhausted = pilot.Job(View(game), new GoldenContext(game.Turn, 3, 71, profile));
        Check(pilot.RemainingLethalNodes == 0 && exhausted.LethalSearch.IsDone && exhausted.LethalResult == "incomplete",
            "abandoned jobs refunded lethal work or claimed exhaustive result");
        var next = pilot.Job(View(game), new GoldenContext(game.Turn + 2, 0, 71, profile));
        Check(((LethalJob)next.LethalSearch).Cap == profile.Budget.LethalNodes && pilot.RemainingLethalNodes == profile.Budget.TurnLethalNodes, "new turn did not reset budget");
    }
    public static void TestClockCutKeepsLegalCachedLineAndUsesLocationFallback()
    {
        var game = Fixture(); game.Player1.UsedMana = 10;
        var body = Board(game, true, "CS2_182", true);
        var state = View(game); var pilot = new GoldenPilot(NpcCatalog.All[0]);
        var context = new GoldenContext(game.Turn, 0, 71, pilot.Profile);
        pilot.Job(state, context);
        var hit = state.Actions.First(a => a.Source == body.Id && a.Target == game.Player2.Hero.Id);
        var end = state.Actions.Single(a => a.Type == PlayerTaskType.END_TURN);
        pilot.Accept(state, new[] { hit, end }, 0, LethalOutcome.Incomplete);
        var child = state.Apply(hit);
        var kept = pilot.Job(child, context).StopSearch(SearchStop.Clock);
        Check(kept.Action.Type == PlayerTaskType.END_TURN && kept.FallbackReason == null && pilot.PlanReused, "clock cut discarded cached plan");
        var location = Entity.FromCard(game.Player1, Cards.FromId("CATA_584"), zone: game.Player1.BoardZone);
        body.IsExhausted = true;
        state = View(game);
        Check(state.OrderedActions.First().Type == PlayerTaskType.END_TURN, "fixture no longer exposes LocalBot gap");
        var fallback = new GoldenPilot(pilot.Profile).Job(state, context).StopSearch(SearchStop.Clock);
        Check(fallback.Action.Type == PlayerTaskType.ACTIVATE_LOCATION && fallback.Action.Source == location.Id,
            "golden fallback left ready Volcano unused");
        Check(LocalBot.Choose(game.Player1.Options()).PlayerTaskType == PlayerTaskType.END_TURN, "legacy fallback changed");
    }
    public static void TestClockCutUsesInProgressBeamWithoutMoreSearch()
    {
        var game = Fixture(); game.Player1.UsedMana = 10;
        Board(game, true, "CS2_182", true); Board(game, true, "CS2_182", true);
        var profile = NpcCatalog.All[0]; var pilot = new GoldenPilot(profile);
        var job = pilot.Job(View(game), new GoldenContext(game.Turn, 0, 71, profile));
        Run(job.LethalSearch);
        job.StepNode(); job.StepNode();
        int lethalLeft = pilot.RemainingLethalNodes;
        var result = job.StopSearch(SearchStop.Clock);
        Check(result.Action.Type == PlayerTaskType.MINION_ATTACK && result.Stop == "Clock" && result.FallbackReason == null,
            "clock cut ignored best in-progress beam");
        Check(job.IsDone && pilot.RemainingLethalNodes == lethalLeft, "clock cut ran extra lethal search");
    }
}
