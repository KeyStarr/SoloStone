using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class DeckObservationTests
{
    private static FairOptions Options() => new FairOptions { TrackOwnDeck = true, MaterializeOwnDeckTriggers = true, RemoveOpponentSecrets = true };
    internal static void Run()
    {
        foreach (int seat in new[] { 1, 2 })
        {
            var game = DriverTests.Fixture(seat, "CS2_182", "CS2_182", "CS2_182", "CS2_182", "CS2_182", "CS2_029", "CS2_024");
            var twin = game.CloneExact();
            twin.PowerHistory.Full.AddRange(game.PowerHistory.Full);
            var bot = game.CurrentPlayer; var other = twin.CurrentPlayer;
            var fire = other.DeckZone.Single(c => c.Card.Id == "CS2_029");
            var frost = other.DeckZone.Single(c => c.Card.Id == "CS2_024");
            (fire.Card, frost.Card) = (frost.Card, fire.Card);
            Generic.DrawFrom(bot.Opponent, bot, game.IdEntityDic[fire.Id]);
            Generic.DrawFrom(other.Opponent, other, fire);
            var options = Options();
            var view = FairBotState.Create(game, seat, 92, DeckStyle.Tempo, options);
            var variant = FairBotState.Create(twin, seat, 92, DeckStyle.Tempo, Options());
            DriverTests.Check(FairViewTests.Fingerprint(view) == FairViewTests.Fingerprint(variant), "Hidden stolen definition changes own-deck knowledge/full view");
            var driver = new DriverOptions { FairViewFactory = (g, id, seed, style) => FairBotState.Create(g, id, seed, style, Options()) };
            DriverTests.Check(DriverTests.Trace(DriverTests.Decide(game, seat, driver).Trace) == DriverTests.Trace(DriverTests.Decide(twin, seat, driver).Trace),
                "Hidden stolen definition changes decision");
            var removed = new HashSet<string>();
            for (int seed = 1; seed <= 120 && removed.Count < 2; seed++)
            {
                var sample = FairBotState.Create(game, seat, seed, DeckStyle.Tempo, Options());
                foreach (string id in new[] { "CS2_029", "CS2_024" })
                    if (!sample.Facts.OwnDeckKnowledge.ContainsKey(id)) removed.Add(id);
            }
            DriverTests.Check(removed.Count == 2, "Unknown theft must sample either definition, not reveal actual removal");

            game = DriverTests.Fixture(seat); bot = game.CurrentPlayer; options = Options();
            var merchant = Generic.DrawCard(bot, Cards.FromId("TLC_514"));
            DriverTests.Check(game.Process(PlayCardTask.Any(bot, merchant)), "Merchant fixture play");
            FairBotState.Create(game, seat, 92, DeckStyle.Tempo, options);
            var offers = bot.Choice.Choices.Select(id => game.IdEntityDic[id]).ToArray();
            var rejected = offers.Skip(1).Select(c => c.Card.Id).ToArray();
            var pending = game.GetPendingDecisions().Single();
            DriverTests.Check(game.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(pending.Id, bot.Id,
                new[] { offers[0].Id }), out string error), "Merchant fixture choice: " + error);
            view = FairBotState.Create(game, seat, 92, DeckStyle.Tempo, options);
            foreach (var group in rejected.GroupBy(id => id))
                DriverTests.Check(view.Facts.OwnDeckKnowledge.TryGetValue(group.Key, out int count) && count >= group.Count(), "Visible rejected Merchant offer missing from ledger");
            DriverTests.Check(FairViewTests.Fingerprint(view) == FairViewTests.Fingerprint(
                FairBotState.Create(game.CloneExact(), seat, 92, DeckStyle.Tempo, Options())),
                "Exact clone lost observed Merchant offers when presentation history was dropped");

            var generated = Generic.DrawCard(bot, Cards.FromId("CS2_029"));
            FairBotState.Create(game, seat, 92, DeckStyle.Tempo, options);
            bot.HandZone.Remove(generated); Generic.ShuffleIntoDeck(bot, merchant, generated);
            view = FairBotState.Create(game, seat, 92, DeckStyle.Tempo, options);
            DriverTests.Check(view.Facts.OwnDeckKnowledge["CS2_029"] == 1, "Observed generated shuffle missing from ledger");
            Generic.Draw(bot, generated);
            view = FairBotState.Create(game, seat, 92, DeckStyle.Tempo, options);
            DriverTests.Check(!view.Facts.OwnDeckKnowledge.ContainsKey("CS2_029"), "Observed draw not removed from ledger");
            // A card taken from the other deck is known only once this player receives it.
            var stolen = Generic.DrawFrom(bot, bot.Opponent);
            string stolenId = stolen.Card.Id;
            FairBotState.Create(game, seat, 92, DeckStyle.Tempo, options);
            int countBefore = view.Facts.OwnDeckKnowledge.TryGetValue(stolenId, out int current) ? current : 0;
            bot.HandZone.Remove(stolen); Generic.ShuffleIntoDeck(bot, merchant, stolen);
            view = FairBotState.Create(game, seat, 92, DeckStyle.Tempo, options);
            DriverTests.Check(view.Facts.OwnDeckKnowledge[stolenId] == countBefore + 1, "Observed stolen shuffle missing from ledger");

            game = DriverTests.Fixture(seat); bot = game.CurrentPlayer; options = Options();
            var formerlySeen = Generic.DrawCard(bot, Cards.FromId("CS2_029"));
            FairBotState.Create(game, seat, 92, DeckStyle.Tempo, options);
            bot.HandZone.Remove(formerlySeen);
            formerlySeen.Controller = bot.Opponent; formerlySeen[GameTag.CONTROLLER] = bot.Opponent.PlayerId;
            bot.Opponent.HandZone.Add(formerlySeen);
            bot.Opponent.HandZone.Remove(formerlySeen);
            formerlySeen.Controller = bot; formerlySeen[GameTag.CONTROLLER] = bot.PlayerId;
            Generic.ShuffleIntoDeck(bot, bot.Opponent.Hero, formerlySeen);
            view = FairBotState.Create(game, seat, 92, DeckStyle.Tempo, options);
            DriverTests.Check(!view.Facts.OwnDeckKnowledge.ContainsKey("CS2_029"),
                "Hidden opponent insertion reused an old observed identity mapping");

            game = DriverTests.Fixture(seat); options = Options();
            var future = game.CloneExact();
            Generic.DrawCard(future.CurrentPlayer, Cards.FromId("CS2_029"));
            FairBotState.Create(future, seat, 92, DeckStyle.Tempo, options);
            game.CurrentPlayer.DeckZone.AddGenerated(Cards.FromId("CS2_024"), game.CurrentPlayer.Opponent.Hero);
            view = FairBotState.Create(game, seat, 92, DeckStyle.Tempo, options);
            DriverTests.Check(!view.Facts.OwnDeckKnowledge.ContainsKey("CS2_029"), "Discarded history branch leaked observation through a reused actor id");
        }
        Console.WriteLine("PASS deck observations: hidden steals, Merchant offers, generated shuffle/draw, both seats");
    }
}
