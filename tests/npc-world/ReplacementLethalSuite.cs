using System;
using System.Threading;

// Regression from review: when a skill variant (IgnoreAoeRisk / LethalMiss) replaced the search job after the scheduler had
// already dequeued the finished lethal probe, the turn runner kept stepping an empty queue until the clock cut in. The runner
// now refreshes its lethal queue from the current job each step. The fixture has plausible raw damage but no legal lethal
// (immune hero), so fresh probes are admitted and must finish.
internal static class ReplacementLethalSuite
{
    internal static void Run()
    {
        bool previous = GoldenPilot.ParallelLethalEnabled;
        try
        {
            GoldenPilot.ParallelLethalEnabled = true;
            var game = PassivitySuite.Fixture(0);
            RoundTwoSuite.Body(game, true, 40, 40);
            game.Player2.Hero.IsImmune = true; // Plausible raw damage, but no legal lethal.
            var view = PassivitySuite.View(game);
            Program.Require(LethalBound.Plausible(view.Facts), "replacement fixture must admit fresh probes");
            var p = NpcCatalog.All[0];
            var budget = new Budget(5000, 100, 32, 1, 0, 160, 8);
            var profile = new NpcProfile(p.Id, p.Version, p.DisplayName, p.DeckId, p.HeroSkin, p.CardBack,
                p.Personality, budget, p.PlanId);
            var skill = new SkillProfile("replacement", 1, 0, 1, budget, 0, 1, 1,
                new[] { new MistakeRule(MistakeKind.IgnoreAoeRisk, 1, 0) });
            var primary = new GoldenPilot(profile);
            var skilled = new SkilledPilot(primary, null, skill, 7);
            var variant = (GoldenPilot)skilled.NoReplyInner;
            bool variantPlanned = false;
            variant.ObservePlanner = _ => variantPlanned = true;
            var guarded = new GuardedSkilledPilot(profile.Id, skilled, primary);
            bool ended = false; GoldenDecision result = null;
            // Simulated: every clock read costs 10 us, so machine load cannot spend the budget (this runs in the gate at load > 100).
            // That leaves ClockCuts catching only an endless stall; MaxReads is what bounds a slow replacement that still finishes.
            // Reads include polling the real-time workers: median 53, max 149 over 800 runs at load up to 293.
            const int MaxReads = 400;
            double extra = 0; long reads = 0; Func<double> clock = () => reads++ * .01 + extra;
            var runner = new NpcTurnRunner(profile, "replacement-lethal", clock,
                () => !ended, () => new GoldenContext(view.Facts.Turn, 1, 71, profile), ctx => guarded.Begin(view, ctx),
                reason => throw new Exception("unexpected replacement fallback " + reason),
                made => { result = made; ended = true; }, () => true);
            runner.Start();
            for (int i = 0; runner.Active && i < 100000; i++)
            {
                runner.Tick();
                if (runner.State == NpcTurnState.Thinking) Thread.Sleep(1); else extra += 100;
            }
            Program.Require(result != null && !runner.Active && runner.ClockCuts == 0,
                "replacement must progress without clock cutoff");
            Program.Require(reads <= MaxReads, "replacement work bound: reads=" + reads + " max=" + MaxReads);
            Program.Require(variantPlanned && !guarded.Disabled && skilled.PerturbFailures == 0,
                "forced IgnoreAoeRisk must finish replacement probe and reach planner");
            Program.Require(result.Nodes == runner.TurnSearchNodes,
                "replacement accounting: result=" + result.Nodes + " runner=" + runner.TurnSearchNodes);
            Console.WriteLine("PASS guarded parallel IgnoreAoeRisk replacement: nodes=" + runner.TurnSearchNodes
                + " clockCuts=" + runner.ClockCuts + " reads=" + reads);
        }
        finally { GoldenPilot.ParallelLethalEnabled = previous; }
    }
}
