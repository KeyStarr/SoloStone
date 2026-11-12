using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Tasks.PlayerTasks;

// plan-reuse and mistakes: a continued plan step has no candidates for Select. With reuse-mistake-rate OFF the decorator
// searches fresh whenever this decision's own rolls could fire (SkilledPilot.RollsCouldFire); by default (ON) it never
// probes, which keeps the calibrated per-game mistake rate (the probe tripled it at skilled, 2026-11-02).
internal static class PlanReuseMistakeSuite
{
    internal static void Run()
    {
        GateIsNecessary();
        ReusedStepRollsMistakes();
        Console.WriteLine("PASS plan-reuse mistake parity");
    }
    // Exactness: whenever the gate is false, Select keeps the best on the most deviation-prone candidate list (three equal
    // plays, every weight 1), for every tier and many decisions.
    private static void GateIsNecessary()
    {
        var game = PassivitySuite.Fixture(10, "CS2_231", "CS2_231", "CS2_231");
        var view = PassivitySuite.View(game);
        var plays = view.Actions.Where(a => a.Type == PlayerTaskType.PLAY_CARD).Take(3).ToArray();
        Program.Require(plays.Length == 3, "fixture: three plays");
        var candidates = plays.Select(a => new KeyValuePair<BotAction, double>(a, 0)).ToList();
        foreach (var skill in SkillCatalog.Parse(Program.Data("skills-v1.json")).Tiers)
        {
            var pilot = new SkilledPilot(new GoldenPilot(NpcCatalog.All[0], PassivitySuite.Plan()), null, skill, 1);
            int gate = 0, deviated = 0;
            for (int index = 1; index <= 2000; index++)
            {
                var context = new GoldenContext(9, index, 71, NpcCatalog.All[0]);
                bool could = pilot.RollsCouldFire(context);
                var choice = SkilledPilot.Select(candidates, skill, pilot.BaseSeed(9, index), new MistakeContext(9, false, 0));
                if (could) gate++;
                if (choice.Index != 0) { deviated++; Program.Require(could, skill.Id + ": Select deviated where the gate said it could not, decision " + index); }
            }
            Program.Require(skill.IsPerfect ? gate == 0 : gate > 0, skill.Id + ": gate rate");
            Console.WriteLine($"  {skill.Id}: gate {gate}/2000, deviations on equal plays {deviated}/2000");
        }
    }
    // With reuse-mistake-rate OFF a continuable step whose rolls could fire runs the bounded probe: some such decisions take
    // a priced mistake (replan=mistake-roll), the rest still continue the plan. Quiet rolls, the default and plan-reuse OFF
    // never probe.
    private static void ReusedStepRollsMistakes()
    {
        var skill = SkillCatalog.Parse(Program.Data("skills-v1.json")).Find("skilled");
        foreach (string mode in new[] { "probe", "default", "plan-reuse-off" })
        {
            bool on = mode == "probe"; int probedMistakes = 0, probedReused = 0, quiet = 0;
            for (int index = 2; index < 60; index++)
            {
                var game = PassivitySuite.Fixture(0);
                RoundTwoSuite.Body(game, true, 2, 2); RoundTwoSuite.Body(game, true, 3, 3); RoundTwoSuite.Body(game, true, 1, 4);
                var profile = mode == "default" ? NpcCatalog.All[0]
                    : NpcCatalog.All[0].WithFixes(new AiFixes(new[] { on ? "reuse-mistake-rate" : "plan-reuse" }));
                var primary = new GoldenPilot(profile, PassivitySuite.Plan());
                var pilot = new SkilledPilot(primary, null, skill, 1);
                var view = PassivitySuite.View(game);
                primary.Job(view, RoundTwoSuite.Context(view, profile));
                var attacks = view.Actions.Where(a => a.Type == PlayerTaskType.MINION_ATTACK && a.Target == game.Player2.Hero.Id).OrderBy(a => a.Source).ToArray();
                primary.Accept(view, new[] { attacks[0], attacks[1], attacks[2], view.Actions.Single(a => a.Type == PlayerTaskType.END_TURN) }, 0, LethalOutcome.None);
                Program.Require(game.Process(attacks[0].Resolve(game.Player1.Options())), "fixture: first attack");
                var next = PassivitySuite.View(game);
                bool could = pilot.RollsCouldFire(new GoldenContext(next.Facts.Turn, index, 71, profile));
                var job = pilot.Begin(next, RoundTwoSuite.Context(next, profile, index));
                for (int i = 0; !job.IsDone && i < 2_000_000; i++) job.StepNode();
                var result = job.Result; var info = result.SearchInfo;
                if (!on || !could)
                {
                    Program.Require(info.Reused && info.Replan == "reused" && result.Action.Equals(attacks[1]) && info.Mistake == "none",
                        (on ? "quiet rolls" : mode) + " continue the plan without a probe: " + info.Replan + " " + info.Mistake);
                    quiet++;
                }
                else if (info.Mistake != "none")
                {
                    Program.Require(!info.Reused && info.Replan == "mistake-roll" && !result.Action.Equals(attacks[1]), "a probed mistake replaces the step: " + result.Action);
                    probedMistakes++;
                }
                else
                {
                    Program.Require(info.Reused && info.Replan == "reused" && result.Action.Equals(attacks[1]), "an unused probe keeps the plan: " + info.Replan);
                    probedReused++;
                }
            }
            if (on) Program.Require(probedMistakes > 0 && quiet > 0, $"probe fixture: mistakes {probedMistakes}, kept {probedReused}, quiet {quiet}");
            Console.WriteLine($"  {mode}: probed mistakes {probedMistakes}, probed kept {probedReused}, no probe {quiet}");
        }
    }
}
