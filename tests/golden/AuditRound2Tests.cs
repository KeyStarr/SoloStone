using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

public static class AuditRound2Tests
{
    private static readonly DeckPlanEvaluator Evaluator = new DeckPlanEvaluator(DeckPlans.DragonWarrior);

    public static void Audit20261031_B2_RejectedClones() => TestRejectedTerminalLeavesReleaseClones();
    public static void TestRejectedTerminalLeavesReleaseClones()
    {
        for (int seed = 1; seed <= 100; seed++) {
            var game = Fixture(CardClass.MAGE); game.Player1.BaseMana = 1; game.Player2.Hero.Health = 3;
            Board(game, false, "CS2_200");
            var missiles = Entity.FromCard(game.Player1, Cards.FromId("EX1_277"), zone: game.Player1.HandZone);
            var root = LethalTests.View(game, seed);
            var cast = root.Actions.Single(a => a.Source == missiles.Id);
            if (!root.Apply(cast).IsTerminal) continue;
            var beam = new TurnBeam(root, s => s.Evaluate(Evaluator), 100, 8, false, Evaluator, dedupe: true);
            Run(beam, 1);
            var rejected = beam.ScoredLeaves.Where(n => n.UnverifiedWin && n.Origin != null).ToArray();
            if (rejected.Length == 0) continue;
            Check(rejected.All(n => !n.Holds), "rejected terminal leaves retain game clones");
            Check(rejected.All(n => n.Value < 999000 && n.HandIds != null && n.EffectsKey != null && n.BoardCount >= 0),
                "rejected leaves must keep ranking summaries");
            Check(rejected.All(n => !n.Holds), "summary access rebuilt rejected clones");
            return;
        }
        throw new Exception("fixture: no rejected lucky terminal leaf");
    }

    public static void Audit20261031_B2_ReplySettlement() => TestPendingDiscoverRandomEndpointIsUncertain();
    public static void TestPendingDiscoverRandomEndpointIsUncertain()
    {
        var game = Fixture(); Board(game, true, "EX1_298"); Board(game, false, "CS2_200");
        game.Player2.Hero.Health = 8;
        var discover = Entity.FromCard(game.Player1, Cards.FromId("EDR_856"), zone: game.Player1.HandZone);
        Check(game.Process(game.Player1.Options().First(t => t.Source?.Id == discover.Id)), "fixture: open Discover");
        for (int seed = 1; seed <= 100; seed++) {
            var root = LethalTests.View(game, seed);
            Check(root.HasPendingChoice && !root.NeedsEndpointProjection(Evaluator), "fixture: capped pending choice skips endpoint");
            var pick = root.OrderedActions.First(); var settled = root.Apply(pick);
            if (settled.ForReplyEndTurn().Facts.Own.PlayState != PlayState.WON) continue;
            var witness = new[] { pick, settled.Actions.Single(a => a.Type == PlayerTaskType.END_TURN) };
            var verify = LethalJob.Verify(root, witness, 100); Run(verify, 1);
            if (verify.Result.Outcome == LethalOutcome.Found) continue;
            var reply = ReplyModel.Job(root, NpcCatalog.All[0], seed, Evaluator, replyNodes: 8);
            reply.StepNode();
            Check(!reply.IsDone, "choice settlement must yield before END_TURN");
            Run(reply, 1);
            Check(reply.Value < 999000, "reply settlement published unverified random win: " + reply.Value);
            var planner = TurnPlanner.Job(root, NpcCatalog.All[0], DeckPlans.DragonWarrior, seed, 2500);
            Run(planner, 1);
            Check(planner.Result.Value < 999000 && planner.Result.Candidates.All(c => c.Value < 999000),
                "planner copied reply settlement certainty");
            return;
        }
        throw new Exception("fixture: no lucky Discover/END_TURN witness rejected by replay");
    }

    public static void Audit20261031_A3_B4_TauntAllocation() => TestThreatAndSurvivalUseBestTauntAllocation();
    public static void TestThreatAndSurvivalUseBestTauntAllocation()
    {
        foreach (bool shield in new[] { false, true }) {
            var game = Fixture(); game.Player1.Hero.Health = 3;
            var taunt = Board(game, true, "CS2_179");
            taunt.HasDivineShield = shield;
            var small = Board(game, false, "CS2_120", true); small.AttackDamage = small.Health = 3;
            var large = Board(game, false, "CS2_200", true); large.AttackDamage = large.Health = 8;
            // A minion-only hit can pop Shield, but cannot add face damage.
            if (shield) { var clear = Board(game, false, "CS2_231", true); clear.CantAttackHeroes = true; }
            var root = View(game); var armored = game.CloneExact(); armored.Player1.Hero.Armor = 2;
            var empty = game.CloneExact();
            foreach (var m in empty.Player2.BoardZone.ToArray()) empty.Player2.SetasideZone.Add(empty.Player2.BoardZone.Remove(m));
            double baseline = View(empty).Evaluate(Evaluator); empty.Player1.Hero.Armor = 2;
            double intrinsicArmor = View(empty).Evaluate(Evaluator) - baseline;
            Check(!DeckPlanEvaluator.PublicBoardSurvives(root.Facts), "B4: 8/8 clears Sen'jin, 3/3 deals lethal; shield=" + shield);
            Check(Math.Abs(View(armored).Evaluate(Evaluator) - root.Evaluate(Evaluator) - intrinsicArmor - 50) < 1e-9,
                "A3: threat must price lethal after optimal Taunt allocation; shield=" + shield);
            Check(game.Process(EndTurnTask.Any(game.Player1)), "fixture: enemy turn");
            if (shield) Check(game.Process(game.Player2.Options().Single(t => t.Source?.Card.Id == "CS2_231" && t.Target == taunt)), "fixture: pop shield");
            Check(game.Process(game.Player2.Options().Single(t => t.Source == large && t.Target == taunt)), "fixture: large clears Taunt");
            Check(game.Process(game.Player2.Options().Single(t => t.Source == small && t.Target == game.Player1.Hero))
                && game.Player1.PlayState == PlayState.LOST, "fixture: small deals lethal");
        }
    }

    public static void TestTauntAllocationCountsShieldWindfuryAndMultipleBlockers()
    {
        foreach (bool reverse in new[] { false, true }) {
            var game = Fixture(); game.Player1.Hero.Health = 11;
            var blocker = Board(game, true, "CS2_179");
            foreach (int attack in reverse ? new[] { 8, 3 } : new[] { 3, 8 }) {
                var body = Board(game, false, "CS2_200", true); body.AttackDamage = attack;
                body.HasWindfury = attack == 8;
            }
            Check(!DeckPlanEvaluator.PublicBoardSurvives(View(game).Facts), "one Windfury charge clears, eleven face damage remains");
            game.Player1.Hero.Health = 12;
            Check(DeckPlanEvaluator.PublicBoardSurvives(View(game).Facts), "Windfury must not invent a third charge");
            blocker.HasDivineShield = true; game.Player1.Hero.Health = 8;
            Check(!DeckPlanEvaluator.PublicBoardSurvives(View(game).Facts), "small hit pops Shield, one big hit clears, second goes face");
            game.Player1.Hero.Health = 9;
            Check(DeckPlanEvaluator.PublicBoardSurvives(View(game).Facts), "Shield must consume a hit");
            blocker.HasDivineShield = false; Board(game, true, "CS2_179");
            game.Player1.Hero.Health = 3;
            Check(!DeckPlanEvaluator.PublicBoardSurvives(View(game).Facts), "two big hits clear two Taunts, small hit deals lethal");
            game.Player1.Hero.Health = 4;
            Check(DeckPlanEvaluator.PublicBoardSurvives(View(game).Facts), "both Taunts must consume attacks");
        }
    }
}
