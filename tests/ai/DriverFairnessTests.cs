using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class DriverFairnessTests
{
    private static IPlayable Play(Game game, string id)
    {
        var card = Generic.DrawCard(game.CurrentPlayer, Cards.FromId(id));
        DriverTests.Check(game.Process(game.CurrentPlayer.Options().First(t => t.Source?.Id == card.Id)), "Fixture play rejected: " + id);
        return card;
    }
    internal static void Run()
    {
        foreach (int seat in new[] { 1, 2 })
        {
            var rewind = DriverTests.Fixture(seat);
            Play(rewind, "TIME_034");
            DriverTests.Check(rewind.CurrentPlayer.Choice?.ChoiceAction == ChoiceAction.REWIND, "Announcer must offer Rewind");
            DriverTests.Pair(rewind, seat, "rewind");
            int calls = 0;
            var options = new DriverOptions { Rewind = view => {
                calls++;
                DriverTests.Check(view.SourceCardId == "TIME_034" && view.OwnWeapon != null && view.OpponentWeapon != null,
                    "Rewind value view must contain source and visible weapons");
                return false;
            }};
            var result = DriverTests.Decide(rewind, seat, options);
            DriverTests.Check(((ChooseTask)result.Task).Choices.Single() == rewind.Rewind.RewindEntityId && calls == 1, "Rewind hook ignored");
            DriverTests.Check(DriverTests.Trace(result.Trace) == DriverTests.Trace(DriverTests.Decide(DriverTests.HiddenVariant(rewind, seat), seat, options).Trace), "Rewind hook sees hidden state");
            options.Rewind = view => true;
            DriverTests.Check(((ChooseTask)DriverTests.Decide(rewind, seat, options).Task).Choices.Single() == rewind.Rewind.KeepEntityId, "Keep hook ignored");

            var selector = DriverTests.Fixture(seat);
            Play(selector, "JAIL_206");
            DriverTests.Check(selector.GetSelectorAnswer() != null, "Dark Bribe must open descriptor selector");
            DriverTests.Pair(selector, seat, "search");

            foreach (string id in new[] { "EDR_856", "EDR_209" })
            {
                var discover = DriverTests.Fixture(seat);
                Play(discover, id);
                DriverTests.Check(discover.CurrentPlayer.Choice != null, "Expected visible offer for " + id);
                var variant = DriverTests.HiddenVariant(discover, seat, false);
                DriverTests.Check(DriverTests.Trace(DriverTests.Decide(discover, seat).Trace) == DriverTests.Trace(DriverTests.Decide(variant, seat).Trace), "Discover/Dark Gift leaks hidden state");
                DriverTests.Parity(discover, seat);
            }

            var attachment = DriverTests.Fixture(seat);
            Play(attachment, "JAIL_205");
            Entity.FromCard(attachment.CurrentPlayer.Opponent, Cards.FromId("CS2_182"), zone: attachment.CurrentPlayer.Opponent.HandZone);
            DriverTests.Pair(attachment, seat, "search");

            var designations = DriverTests.Fixture(seat);
            Play(designations, "EDR_780");
            var changed = DriverTests.HiddenVariant(designations, seat);
            foreach (var payload in changed.IdEntityDic.Values.Where(e => PayloadRules.IsStored(e) && e[PayloadRules.Key] == NativeTailRules.IllusionKey))
                payload[PayloadRules.Value] = 1 - payload[PayloadRules.Value];
            foreach (var player in new[] { designations.Player1, designations.Player2 }) player.Hero[TurnQueueRules.TomeSelection] = 1;
            foreach (var player in new[] { changed.Player1, changed.Player2 }) player.Hero[TurnQueueRules.TomeSelection] = 999;
            var enemyA = designations.CurrentPlayer.Opponent; var enemyB = changed.CurrentPlayer.Opponent;
            enemyA[(GameTag)32804] = enemyB[(GameTag)32804] = 1;
            enemyA[(GameTag)60000] = Cards.FromId("CS2_182").AssetId; enemyB[(GameTag)60000] = Cards.FromId("CS2_029").AssetId;
            enemyA.DeckZone.First()[StartOfGameRules.Pending] = 1;
            enemyB.DeckZone.First()[StartOfGameRules.Pending] = -1;
            var a = FairBotState.Create(designations, seat, 71, DeckStyle.Tempo);
            var b = FairBotState.Create(changed, seat, 71, DeckStyle.Tempo);
            DriverTests.Check(FairViewTests.Fingerprint(a) == FairViewTests.Fingerprint(b), "Private opening/Illusion/Tome/start tags survive masking");
            DriverTests.Check(DriverTests.Trace(DriverTests.Decide(designations, seat).Trace) == DriverTests.Trace(DriverTests.Decide(changed, seat).Trace), "Private designation changes driver trace");
        }
        var clockGame = DriverTests.Fixture();
        var state = FairBotState.Create(clockGame, 1, 71, DeckStyle.Tempo);
        var clock = BotSearch.Choose(state, new BotBudget(4, 10, 600, 0));
        DriverTests.Check(clock.Stop == SearchStop.Clock && clock.Nodes == 0, "Immediate clock cap not reported");
        var depth = BotSearch.Choose(state, new BotBudget(1, 10, 600, int.MaxValue));
        DriverTests.Check(depth.Stop == SearchStop.Depth, "Depth cap not reported");
        var ordered = depth.Candidates.Select(c => c.Key.ToString()).ToArray();
        DriverTests.Check(ordered.SequenceEqual(ordered.OrderBy(x => x, StringComparer.Ordinal))
            && depth.Candidates.Single(c => c.Key.Equals(depth.Action)).Value == depth.Value, "Root candidates lose best value or ordinal ordering");
        clockGame.CurrentPlayer.UsedMana = 10;
        var forced = BotSearch.Choose(FairBotState.Create(clockGame, 1, 71, DeckStyle.Tempo), BotBudget.ForStars(0).WithoutClock());
        DriverTests.Check(forced.Stop == SearchStop.Forced && forced.Action.Type == PlayerTaskType.END_TURN, "Single action not forced");
        var forcedClock = BotSearch.Choose(FairBotState.Create(clockGame, 1, 71, DeckStyle.Tempo), new BotBudget(4, 10, 600, 0));
        DriverTests.Check(forcedClock.Stop == SearchStop.Clock, "Forced-action telemetry must not hide clock cuts");
        DriverTests.Check(BotDriver.Timeout(EndTurnTask.Any(clockGame.CurrentPlayer)).Trace.Kind == "timeout", "Host timeout trace missing");
        var draw = DriverTests.Fixture();
        draw.CurrentPlayer.UsedMana = 7;
        Entity.FromCard(draw.CurrentPlayer, Cards.FromId("CS2_023"), zone: draw.CurrentPlayer.HandZone);
        DriverTests.Check(BotSearch.Choose(FairBotState.Create(draw, 1, 71, DeckStyle.Tempo), new BotBudget(8, 20, 600, int.MaxValue)).Stop == SearchStop.Exhausted,
            "Unknown draw/end-turn frontier must report Exhausted");
        int index = 0;
        var filtered = BotDriver.Decide(clockGame, 1, DeckStyle.Tempo,
            s => PilotDecision.From(BotSearch.Choose(s, BotBudget.ForStars(0).WithoutClock())), ref index,
            nativeActionEnabled: t => t.PlayerTaskType == PlayerTaskType.END_TURN);
        DriverTests.Check(filtered.Task.PlayerTaskType == PlayerTaskType.END_TURN && index == 1, "Native filter or decision index lost");
        var filterGame = DriverTests.Fixture();
        Entity.FromCard(filterGame.CurrentPlayer, Cards.FromId("CS2_029"), zone: filterGame.CurrentPlayer.HandZone);
        filterGame.Player2.Hero.Damage = 24;
        var filterBefore = filterGame.SaveSnapshot(); index = 0;
        filtered = BotDriver.Decide(filterGame, 1, DeckStyle.Tempo,
            s => PilotDecision.From(BotSearch.Choose(s, BotBudget.ForStars(0).WithoutClock())), ref index,
            nativeActionEnabled: t => t.PlayerTaskType == PlayerTaskType.END_TURN);
        DriverTests.Check(filtered.Trace.Kind == "fallback" && filtered.Task.PlayerTaskType == PlayerTaskType.END_TURN
            && filterBefore.SequenceEqual(filterGame.SaveSnapshot()), "Filtered lethal must resolve through legal fallback without mutating authority");
        Console.WriteLine("PASS driver fairness: Rewind hook, selector, Discover/Dark Gift, private history, opening/Illusion/Tome/start tags; stop/candidate telemetry");
    }
}
