using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

public static class DeathrattleReproTests
{
    // Playtest game, Hunter NPC TinkerTempo, turn 18: seven minions on board, six mana left, Dig for Freedom in hand,
    // and the NPC passed. The evaluator gave a flat bonus to any minion with a Deathrattle flag, so Dig (which stacks
    // a second Deathrattle) scored below passing. This is the board shape and stats of that turn, not an exact replay.
    // Minimal turn-18 equivalent: seven bodies, six mana left, Dig held, no enemy minions.
    // Exhaust attacks to isolate the spell decision from the intentionally unchanged mistake layer.
    public static void Audit20261031_D1_DigBeatsPassingOnFullBoard() => TestDigBeatsPassingOnFullBoard();
    public static void TestDigBeatsPassingOnFullBoard()
    {
        var g = Fixture(CardClass.HUNTER); g.Turn = 18;
        g.Player1.BaseMana = 9; g.Player1.UsedMana = 3; g.Player2.BaseMana = 10;
        g.Player1.Hero.Damage = 1; g.Player2.Hero.Health = 22;
        string[] ids = { "CATA_210t", "TIME_609t1", "EDR_485", "CATA_550t", "CATA_550t", "CORE_GIL_623", "DINO_434" };
        int[] attack = { 5, 4, 1, 2, 2, 3, 1 }, health = { 4, 6, 1, 1, 1, 12, 1 };
        for (int i = 0; i < ids.Length; i++) { var m = Board(g, true, ids[i]); m.AttackDamage = attack[i]; m.Health = health[i]; }
        var dig = Entity.FromCard(g.Player1, Cards.FromId("JAIL_876"), zone: g.Player1.HandZone);
        var state = View(g); var evaluator = new DeckPlanEvaluator(DeckPlans.DragonWarrior);
        var pass = state.Actions.Single(a => a.Type == PlayerTaskType.END_TURN);
        double end = state.Apply(pass).Evaluate(evaluator);
        double play = state.Actions.Where(a => a.Source == dig.Id).Max(a => state.Apply(a).ForReplyEndTurn().Evaluate(evaluator));
        Console.WriteLine("Dig full board: play=" + play.ToString("F3") + " end=" + end.ToString("F3"));
        Check(play > end, "Dig must retain more value than its hand card: " + play + " <= " + end);
        var job = TurnPlanner.Job(state, NpcCatalog.All[0], DeckPlans.DragonWarrior, 19403, 1926); Run(job);
        Check(job.Result.Line.Any(a => a.Source == dig.Id), "planner must play Dig: " + PlannerTests.Describe(job.Result));
    }
}
