using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class RoundTwoDistribution
{
    private static double Quantile(List<double> values, double q) => values.Count == 0 ? 0 : values.OrderBy(x => x).ElementAt((int)((values.Count - 1) * q));
    internal static void Run()
    {
        var skills = SkillCatalog.Parse(Program.Data("skills-v1.json"));
        var library = NpcDeckLibrary.Parse(Program.Data("decks-v1.json"));
        foreach (string fixture in new[] { "free-block", "master-trades", "sticky-hold", "owl", "weapon", "early-armor" })
        {
            var game = PassivitySuite.Fixture(fixture == "free-block" ? 0 : fixture == "weapon" ? 3 : 2,
                fixture == "free-block" ? "CORE_EX1_606" : fixture == "weapon" ? "TLC_478" : "TLC_602");
            if (fixture == "free-block") game.Player1.HandZone[0][GameTag.COST] = 0;
            var view = fixture == "master-trades" ? RoundTwoSuite.TradeView() : fixture == "sticky-hold" ? RoundTwoSuite.HoldView()
                : fixture == "owl" ? RoundTwoSuite.OwlView() : PassivitySuite.View(game);
            var plan = fixture == "sticky-hold" ? AutoDeckPlan.Derive(library.Find("npc-death-knight-aggro"))
                : fixture == "owl" ? AutoDeckPlan.Derive(library.Find("npc-hunter-quest")) : PassivitySuite.Plan();
            var profile = RoundTwoSuite.Profile();
            var best = RoundTwoSuite.Search(view, plan, profile);
            foreach (var skill in skills.Tiers)
            {
                var losses = new List<double>(); var priced = new List<double>(); var milliseconds = new List<double>();
                var counts = Enum.GetValues(typeof(MistakeKind)).Cast<MistakeKind>().ToDictionary(k => k, k => 0);
                int passes = 0, failures = 0, noise = 0, completed = 0, excess = 0;
                for (int seed = 0; seed < 200; seed++)
                {
                    // Keep search budget and replies identical across tiers; measure the decorator, not elite's larger budget.
                    var pilot = new SkilledPilot(new GoldenPilot(profile, plan), null, skill, seed);
                    var state = view; int decisionIndex = 0; bool deviated = false; double price = 0, bound = 0;
                    while (state.CanExpand && decisionIndex < 20)
                    {
                        var watch = Stopwatch.StartNew();
                        var decision = pilot.Decide(state, RoundTwoSuite.Context(state, profile, ++decisionIndex));
                        milliseconds.Add(watch.Elapsed.TotalMilliseconds);
                        if (decisionIndex == 1)
                        {
                            deviated = !decision.Action.Equals(best.Line[0]);
                            if (decision.Action.Type == PlayerTaskType.END_TURN && best.Line[0].Type != PlayerTaskType.END_TURN) passes++;
                        }
                        if (pilot.LastMistakeLine != null) { price += pilot.LastMistakeLoss; bound += pilot.LastMistakeBound; }
                        // Re-enter the actual wrapper for every action, including subsequent sticky-hold decisions.
                        state = state.Apply(decision.Action);
                    }
                    if (!state.CanExpand) completed++;
                    failures += pilot.PerturbFailures; noise += pilot.SoftmaxDeviations;
                    foreach (var pair in pilot.MistakeCounts) counts[pair.Key] += pair.Value;
                    if (!deviated && pilot.MistakeCounts.Values.Sum() == 0 && pilot.SoftmaxDeviations == 0) continue;
                    double loss = Math.Max(0, best.Value - state.Evaluate(new DeckPlanEvaluator(plan)));
                    losses.Add(loss); priced.Add(price);
                    if (loss > bound + 1e-8) excess++;
                }
                Console.WriteLine(PlaytestRecord.Serialize(new Dictionary<string, object> {
                    { "fixture", fixture }, { "tier", skill.Id }, { "trials", 200 }, { "completed", completed }, { "deviations", losses.Count },
                    { "passes", passes }, { "kinds", counts.ToDictionary(p => p.Key.ToString(), p => p.Value) }, { "softmax", noise },
                    { "meanLoss", losses.Count == 0 ? 0 : losses.Average() }, { "p95Loss", Quantile(losses, .95) }, { "maxLoss", Quantile(losses, 1) },
                    { "meanPricedLoss", priced.Count == 0 ? 0 : priced.Average() }, { "boundFailures", excess }, { "failures", failures },
                    { "decisions", milliseconds.Count }, { "medianMs", Quantile(milliseconds, .5) }, { "p95Ms", Quantile(milliseconds, .95) }, { "maxMs", Quantile(milliseconds, 1) } }));
                Program.Require(completed == 200 && failures == 0 && excess == 0, "completed-turn distribution bounds: " + fixture + "/" + skill.Id);
            }
        }
    }
}
