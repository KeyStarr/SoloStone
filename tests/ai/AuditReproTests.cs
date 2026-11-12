using System.Reflection;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

// AUDIT=1 runs each proof independently and returns nonzero when any correct-behavior assertion fails.
internal static class AuditReproTests
{
    internal static int Run(string[] filters)
    {
        var tests = typeof(AuditReproTests).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name.StartsWith("Audit20261031_") && (filters.Length == 0 || filters.Any(f => m.Name.Contains(f))))
            .OrderBy(m => m.Name).ToArray();
        int failed = 0;
        foreach (var test in tests) {
            try { test.Invoke(null, null); Console.WriteLine("PASS " + test.Name); }
            catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + test.Name + ": " + (error.InnerException ?? error)); }
        }
        Console.WriteLine($"{tests.Length - failed}/{tests.Length} audit tests passed");
        return failed == 0 && tests.Length > 0 ? 0 : 1;
    }

    public static void Audit20261031_C1_SelectorBanishesBothThreats()
    {
        var game = DriverTests.Fixture();
        var token = Entity.FromCard(game.Player2, Cards.FromId("CS2_231"), zone: game.Player2.BoardZone);
        var first = (Minion)Entity.FromCard(game.Player2, Cards.FromId("CS2_200"), zone: game.Player2.BoardZone);
        var second = (Minion)Entity.FromCard(game.Player2, Cards.FromId("CS2_200"), zone: game.Player2.BoardZone);
        first.AttackDamage = second.AttackDamage = 8; first.Health = second.Health = 8;
        var osk = Entity.FromCard(game.Player1, Cards.FromId("TLC_452t21"), zone: game.Player1.HandZone);
        DriverTests.Check(game.Process(game.Player1.Options().Single(t => t.Source == osk && t.Target == first)), "fixture: Osk first target");
        DriverTests.Check(game.GetSelectorAnswer() != null && game.Player1.Choice.Choices.Contains(second.Id), "fixture: second 8/8 must be eligible");
        var pilot = new GoldenPilot(NpcCatalog.All[0]); int index = 0;
        var result = BotDriver.Decide(game, 1, DeckStyle.Tempo, view => pilot.DecidePilot(view,
            new GoldenContext(game.Turn, 0, 71, pilot.Profile)), ref index, pilot.Options);
        var selected = ((ChooseTask)result.Task).Choices.Single();
        var view = pilot.Options.FairViewFactory(game, 1, 71, DeckStyle.Tempo);
        var pending = game.GetPendingDecisions().Single();
        DriverTests.Check(game.TryAnswer(new DecisionAnswer(pending.Id, pending.ControllerId, new[] { selected }), out string error),
            "fixture: selector answer must execute: " + error);
        DriverTests.Check(selected == second.Id && game.Player2.BoardZone.Count == 1 && game.Player2.BoardZone[0].Id == token.Id,
            "selector selected=" + selected + " token=" + token.Id + " remaining 8/8=" + second.Id + " path=" + result.Trace.Kind + " fair choices=" + view.Actions.Count());
    }

    private static Game SecretFixture(string id)
    {
        var game = DriverTests.Fixture(); game.Player1.Hero.Damage = 25;
        var secret = Entity.FromCard(game.Player1, Cards.FromId(id), zone: game.Player1.HandZone);
        DriverTests.Check(game.Process(PlayCardTask.Spell(game.Player1, secret)) && secret.ActivatedTrigger != null, "fixture: secret armed");
        var attacker = (Minion)Entity.FromCard(game.Player2, Cards.FromId("CS2_200"), zone: game.Player2.BoardZone);
        attacker.AttackDamage = 6; attacker.IsExhausted = false;
        return game;
    }
    private static (int Life, PlayState State, string Secret) ReplyAttack(Game game)
    {
        var pilot = new GoldenPilot(NpcCatalog.All[0]);
        var view = pilot.Options.FairViewFactory(game, 1, 71, DeckStyle.Tempo);
        var ended = view.ForReplyEndTurn();
        var reply = ended.Determinize(Enumerable.Repeat("CS2_182", ended.Facts.Opponent.Hand.Count(c => c.Unknown)).ToArray(), 5).ForReply();
        string secret = FairViewTests.Copy(reply).Player1.SecretZone.Single().Card.Id;
        var hit = reply.Actions.Single(a => a.Type == PlayerTaskType.MINION_ATTACK && a.Target == reply.Facts.Opponent.Hero.EntityId);
        var after = reply.Apply(hit);
        return (after.Facts.Opponent.Hero.Health + after.Facts.Opponent.Hero.Armor, after.Facts.Opponent.PlayState, secret);
    }
    private static (int Life, PlayState State) RealAttack(Game game)
    {
        game = game.CloneExact(); DriverTests.Check(game.Process(EndTurnTask.Any(game.Player1)), "fixture: human turn");
        DriverTests.Check(game.Process(game.Player2.Options().Single(a => a.PlayerTaskType == PlayerTaskType.MINION_ATTACK && a.Target == game.Player1.Hero)), "fixture: authoritative six damage");
        return (game.Player1.Hero.Health + game.Player1.Hero.Armor, game.Player1.PlayState);
    }
    public static void Audit20261031_C4_ReplyPreservesKnownSecretOutcome()
    {
        var barrier = SecretFixture("CORE_EX1_289"); var counter = SecretFixture("CORE_EX1_287");
        var realBarrier = RealAttack(barrier); var realCounter = RealAttack(counter);
        DriverTests.Check(realBarrier.Life == 7 && realBarrier.State != PlayState.LOST && realCounter.State == PlayState.LOST,
            "fixture: Ice Barrier survives; Counterspell dies");
        var a = ReplyAttack(barrier); var b = ReplyAttack(counter);
        DriverTests.Check(a.Life == realBarrier.Life && a.State == realBarrier.State && b.State == realCounter.State,
            $"actual Barrier life={realBarrier.Life}, Counterspell={realCounter.State}; replies Barrier={a.Life}/{a.State}/{a.Secret}, Counterspell={b.Life}/{b.State}/{b.Secret}");
    }
}
