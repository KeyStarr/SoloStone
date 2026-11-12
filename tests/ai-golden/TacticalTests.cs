using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

public static class TacticalTests
{
    public static void TestBeamRetainsAttacksWithoutSpendingHand()
    {
        var g = Fixture(); g.Turn = 15;
        for (int i = 0; i < 3; i++) { var m = Board(g, true, "CS2_120", true); m.Damage = 1; }
        Entity.FromCard(g.Player1, Cards.FromId("EDR_457"), zone: g.Player1.HandZone);
        Entity.FromCard(g.Player1, Cards.FromId("CAP_107"), zone: g.Player1.HandZone);
        var evaluator = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
        var beam = new TurnBeam(View(g), s => s.Evaluate(evaluator), 477, 6, false, evaluator); Run(beam);
        Check(beam.Leaves.Any(n => n.State.Facts.Own.Hand.Count == 2 && n.Actions.Count(a => a.Type == PlayerTaskType.MINION_ATTACK) == 3
            && !n.Actions.Any(a => a.Type == PlayerTaskType.PLAY_CARD)), "beam must compare attack-then-hold with development");
    }
    public static void TestPlannerComparesDifferentCardsAtSameBoardSize()
    {
        var g = Fixture(); g.Turn = 15; g.Player1.BaseMana = 4;
        var slitherdrake = Entity.FromCard(g.Player1, Cards.FromId("END_033"), zone: g.Player1.HandZone);
        var dragon = Entity.FromCard(g.Player1, Cards.FromId("CATA_556"), zone: g.Player1.HandZone);
        for (int i = 0; i < 3; i++) Entity.FromCard(g.Player2, Cards.FromId("CS2_182"), zone: g.Player2.HandZone);
        foreach (int slice in new[] { 1, 7, 1000 }) {
            var job = TurnPlanner.Job(View(g), NpcCatalog.All[0], DeckPlans.DragonWarrior, 14986); Run(job, slice);
            Check(job.Result.Candidates.Any(c => c.Key.Source == slitherdrake.Id) && job.Result.Candidates.Any(c => c.Key.Source == dragon.Id),
                "compare Slitherdrake and Whelp lines even when both produce one minion");
            Check(job.Result.Nodes <= NpcCatalog.All[0].Budget.PlanNodes, "candidate diversity must fit existing budget");
        }
    }
    public static void TestEnemyWideDamageIsAreaThreat()
    {
        Check(new CardFrequency("CORE_CS2_093", 1).AreaThreat, "Consecration damages all enemies");
        Check(new CardFrequency("CORE_CS1_112", 1).AreaThreat, "Holy Nova damages all enemies");
    }
    public static void TestDiscountDoesNotDestroyHandValue()
    {
        var g = Fixture();
        Entity.FromCard(g.Player1, Cards.FromId("END_033"), zone: g.Player1.HandZone);
        var evaluator = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
        var alone = View(g).Facts.Own.Hand;
        Entity.FromCard(g.Player1, Cards.FromId("CATA_556"), zone: g.Player1.HandZone);
        var pair = View(g).Facts.Own.Hand.Where(c => c.CardId == "END_033");
        Check(evaluator.Hand(pair) >= evaluator.Hand(alone), "discounting Slitherdrake must not lower its resource value");
    }
    public static void TestDragonCannotEnableItsOwnHold()
    {
        var g = Fixture();
        Entity.FromCard(g.Player1, Cards.FromId("END_033"), zone: g.Player1.HandZone);
        var evaluator = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
        var alone = View(g);
        Entity.FromCard(g.Player1, Cards.FromId("CATA_556"), zone: g.Player1.HandZone);
        var pair = View(g);
        double change = pair.Evaluate(evaluator) - alone.Evaluate(evaluator);
        double handChange = evaluator.Hand(pair.Facts.Own.Hand) - evaluator.Hand(alone.Facts.Own.Hand);
        Check(change > handChange, "another Dragon must enable hold value absent with Slitherdrake alone");
    }
}
