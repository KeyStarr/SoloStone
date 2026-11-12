using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Tasks.PlayerTasks;

// Mistake pricing with a queued extra turn (ExtraTurn: NumTurnsLeft = 2). The planner used to rebuild released lines by
// applying END_TURN, so with an extra turn queued the retained and released lines looked at different horizons and a 6.7-point
// loss passed a bound of 5. END_TURN is now kept as a sentinel; retained and released scores must match, and the hold must
// be rejected by its real loss.
internal static class RoundThreeSuite
{
    private static FairBotState ExtraTurn()
    {
        var game = PassivitySuite.Fixture(3, "CS2_120");
        game.Player1.Hero.HeroPower.IsExhausted = true;
        game.Player1.NumTurnsLeft = 2;
        return PassivitySuite.View(game);
    }
    private static PlannedTurn Search(FairBotState view, bool hold, BotAction first = null)
    {
        bool previous = GoldenAblation.HoldLineStates;
        GoldenAblation.HoldLineStates = hold;
        try
        {
            var job = TurnPlanner.Job(view, RoundTwoSuite.Profile(cap: 7000), PassivitySuite.Plan(), 71, firstAction: first);
            while (!job.IsDone) job.StepNode();
            Program.Require(job.Result.Stop == SearchStop.Exhausted, "extra-turn search completes");
            return job.Result;
        }
        finally { GoldenAblation.HoldLineStates = previous; }
    }
    private static void RetainedAndReleasedPass()
    {
        var view = ExtraTurn(); var end = view.Actions.Single(a => a.Type == PlayerTaskType.END_TURN);
        var retained = Search(view, true, end); var released = Search(view, false, end);
        Program.Require(retained.Line.Count == 1 && retained.Line[0].Equals(end)
            && released.Line.SequenceEqual(retained.Line), "forced pass keeps the END_TURN line");
        Console.WriteLine("Extra-turn pass: retained=" + retained.Value.ToString("R") + " released=" + released.Value.ToString("R"));
        Program.Require(Math.Abs(retained.Value - released.Value) < 1e-9,
            "retained/released END_TURN reply scores must match with a queued extra turn");
        Console.WriteLine("PASS extra-turn retained/released END_TURN scores");
    }
    private static void HoldBound()
    {
        var view = ExtraTurn(); var profile = RoundTwoSuite.Profile(cap: 7000); var plan = PassivitySuite.Plan();
        var retained = Search(view, true);
        var end = view.Actions.Single(a => a.Type == PlayerTaskType.END_TURN);
        double pass = Search(view, true, end).Value;
        double loss = retained.Value - pass;
        Program.Require(retained.Line[0].Type == PlayerTaskType.PLAY_CARD && loss > 5,
            "extra-turn fixture supplies a hold above the five-point bound: " + loss);
        // Admit an optimistic selector price; the real paired continuation must still reject the hold.
        var raw = new GoldenDecision(retained.Line[0], retained.Value, retained.Nodes, retained.Line.Count,
            "Exhausted", null, new[] { new KeyValuePair<BotAction, double>(retained.Line[0], retained.Value),
                new KeyValuePair<BotAction, double>(end, retained.Value - .01) });
        var skill = new SkillProfile("extra-turn-hold", 1, 0, 1, profile.Budget, 0, 1, 1,
            new[] { new MistakeRule(MistakeKind.HoldTooLong, 1, 5) });
        var pilot = new SkilledPilot(skill, 0, plan, v => new RoundTwoSuite.FixedPilot(raw), "extra-turn-hold");
        var selected = pilot.Decide(view, RoundTwoSuite.Context(view, profile));
        Console.WriteLine("Extra-turn hold: actualLoss=" + loss.ToString("R") + " reportedLoss=" + pilot.LastMistakeLoss.ToString("R")
            + " selected=" + selected.Action);
        Program.Require(selected.Action.Equals(raw.Action) && pilot.MistakeCount(MistakeKind.HoldTooLong) == 0
            && pilot.PerturbFailures == 0, "extra-turn hold cannot underprice a loss above five points");
        Console.WriteLine("PASS extra-turn hold continuation bound");
    }
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (var check in new Action[] { RetainedAndReleasedPass, HoldBound })
            try { check(); } catch (Exception error) { failures.Add(error.Message); Console.WriteLine(error.Message); }
        Program.Require(failures.Count == 0, "round-3 regressions: " + failures.Count);
    }
}
