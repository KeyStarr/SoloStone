using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

public static class MinimumAnswerTests
{
    public static void TestMinimumAnswerCanRationallyPass()
    {
        var game = Fixture(SabberStoneCore.Enums.CardClass.MAGE);
        game.Player1.Hero.HeroPower.IsExhausted = true;
        Entity.FromCard(game.Player1, Cards.FromId("CS2_032"), zone: game.Player1.HandZone);
        var state = View(game); var minimum = new BoundedFallbackJob(state, state.Actions);
        while (!minimum.IsDone) minimum.StepNode();
        Check(minimum.Nodes > 0 && minimum.Result.Type == PlayerTaskType.END_TURN,
            "minimum answer forced a wasteful spell instead of retaining evaluated pass");
    }
    public static void TestFirstPlannerNodeCannotDiscardPreparedAnswer()
    {
        var game = Fixture(); game.Player1.Hero.HeroPower.IsExhausted = true;
        var blocked = Cards.FromId("CS2_200").Clone();
        blocked.Power = new SabberStoneCore.Enchants.Power {
            PowerTask = new SabberStoneCore.Tasks.SimpleTasks.FuncNumberTask((IPlayable source) => {
                throw new NotSupportedException("Private historical controlled-first-planner-node");
            }) };
        var bad = Entity.FromCard(game.Player1, blocked, zone: game.Player1.HandZone); bad.Cost = 0;
        var good = Entity.FromCard(game.Player1, Cards.FromId("CS2_231"), zone: game.Player1.HandZone); good.Cost = 0;
        var view = View(game); var original = NpcCatalog.All[0];
        var profile = new NpcProfile(original.Id, original.Version, original.DisplayName, original.DeckId,
            original.HeroSkin, original.CardBack, original.Personality, new Budget(1000, 1000, 0, 1, 1, 1), original.PlanId);
        var job = new GoldenPilot(profile).Job(view, new GoldenContext(game.Turn, 1, 71, profile));
        while (job.Preparing) job.StepNode();
        job.StepNode(); // First planner expansion reaches the controlled private-history boundary.
        var stopped = job.StopSearch(SearchStop.Clock);
        Check(stopped.Action.Type == PlayerTaskType.PLAY_CARD && view.Facts.Own.Hand.Single(c => c.EntityId == stopped.Action.Source).CardId == "CS2_231",
            "empty planner discarded evaluated minimum: " + stopped.Action);
    }
    public static void TestClockCutKeepsEvaluatedMoveBeforeLethalCompletes()
    {
        try
        {
            foreach (bool parallel in new[] { false, true })
            { GoldenPilot.ParallelLethalEnabled = parallel; ClockCutKeepsEvaluatedMove(); }
        }
        finally { GoldenPilot.ParallelLethalEnabled = false; }
    }
    private static void ClockCutKeepsEvaluatedMove()
    {
        var game = Fixture();
        game.Player1.BaseMana = 15;
        for (int i = 0; i < 9; i++) Entity.FromCard(game.Player1, Cards.FromId("CS2_182"), zone: game.Player1.HandZone).Cost = 0;
        for (int i = 0; i < 6; i++) Board(game, false, "CS2_231");
        var view = View(game);
        var original = NpcCatalog.All[0]; var b = original.Budget;
        var profile = new NpcProfile(original.Id, original.Version, original.DisplayName, original.DeckId,
            original.HeroSkin, original.CardBack, original.Personality,
            new Budget(20000, 10000, 10000, b.ReplyK, b.ReplyL, b.ReplyR, b.OwnBeamWidth, b.Dedupe, 45, 1), original.PlanId);
        var pilot = new GoldenPilot(profile);
        double now = 0; GoldenDecision decision = null; int fallbackEvaluations = 0;
        NpcTurnRunner runner = null;
        runner = new NpcTurnRunner(profile, "expired-lethal", () => now += 35, () => decision == null,
            () => new GoldenContext(game.Turn, 1, 71, profile), ctx => pilot.Job(view, ctx),
            reason => {
                var chosen = BotDriver.BoundedFallback(view, view.Actions, () => {
                    if (!runner.SpendFallbackNode()) return false;
                    fallbackEvaluations++; return true;
                });
                return new GoldenDecision(chosen, 0, 0, 0, "Forced", reason, null, "fallback");
            }, result => decision = result, () => true);
        runner.Start(); runner.RunSynchronously();
        Check(runner.ClockCut && pilot.LastLethal == LethalOutcome.Incomplete, "fixture did not cut an incomplete lethal");
        Check(decision.Action.Type == PlayerTaskType.PLAY_CARD,
            "clock cutoff discarded all usable alternatives: " + decision.Action + "; fallback evaluated=" + fallbackEvaluations);
        Check(view.Actions.Contains(decision.Action), "minimum answer is illegal");
        Check(runner.SearchMilliseconds < 2000, "minimum answer added unbounded overtime");
    }
}
