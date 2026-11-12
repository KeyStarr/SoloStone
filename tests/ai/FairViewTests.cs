using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using System.Reflection;
using static DriverTests;

internal static class FairViewTests
{
    internal static Game Copy(FairBotState state) => (Game)typeof(FairBotState).GetField("game", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(state);
    internal static string FactsFingerprint(FairBotState state) => System.Text.Json.JsonSerializer.Serialize(state.Facts,
        new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
    internal static string Fingerprint(FairBotState state) => Fingerprint(Copy(state)) + "|facts:" + FactsFingerprint(state);
    private static string Fingerprint(Game game) => string.Join("|", game.IdEntityDic.Values.OrderBy(c => c.Id).Select(c =>
        c.Id + ":" + c.Card.Id + ":" + c.Zone?.Type + ":" + string.Join(",", c.NativeTags.OrderBy(t => (int)t.Key).Select(t => (int)t.Key + "=" + t.Value))
        + ":branches:" + string.Join(",", (c.ChooseOnePlayables ?? Array.Empty<IPlayable>()).Select(b => b == null ? "null" : b.Id + "=" + b.Card.Id))))
        + game.HistoryFacts.CanonicalText();
    internal static FairBotState Create(Game game, int owner, int seed, DeckStyle style)
    {
        var state = FairBotState.Create(game, owner, seed, style);
        DriverTests.Check(state.Describe() == FairBotState.Create(game, owner, seed, style, null).Describe()
            && Fingerprint(state) == Fingerprint(FairBotState.Create(game, owner, seed, style, new FairOptions())), "Default fair options changed view");
        if (state.Actions.Length != 0) DriverTests.Parity(game, owner);
        return state;
    }
    private sealed class CountEvaluator : IStateEvaluator
    {
        public double Evaluate(FairFacts facts) => facts.OpponentSecretCount + facts.Own.Hand.Count * 10 + facts.Own.Mana * 100;
    }
    internal static void Run()
    {
        PrivateBranchTests.Run();
        DeckObservationTests.Run();
        foreach (int viewer in new[] { 1, 2 })
        {
            var fresh = Game.LoadSnapshot(DriverTests.Fixture().SaveSnapshot());
            var bytes = fresh.SaveSnapshot();
            FairBotState.Create(fresh, viewer, 71, DeckStyle.Tempo);
            DriverTests.Check(bytes.SequenceEqual(fresh.SaveSnapshot()), "Fair view warmed authoritative opponent cache");
        }
        foreach (int seat in new[] { 1, 2 })
        {
            var game = DriverTests.Fixture(seat); var bot = game.CurrentPlayer;
            Entity.FromCard(bot.Opponent, Cards.FromId("EX1_295"), zone: bot.Opponent.SecretZone);
            var options = new DriverOptions { FairViewFactory = (g, id, seed, style) => FairBotState.Create(g, id, seed, style,
                new FairOptions { RemoveOpponentSecrets = true }) };
            DriverTests.Check(Decide(game, seat, options).Trace.Kind == "search", "Golden secret factory must run before rejection");
            var variant = HiddenVariant(game, seat);
            var enemy = variant.CurrentPlayer.Opponent;
            var old = enemy.SecretZone.Single(); enemy.SecretZone.Remove(old);
            Entity.FromCard(enemy, Cards.FromId("EX1_287"), zone: enemy.SecretZone, id: old.Id);
            var before = game.SaveSnapshot();
            var state = options.FairViewFactory(game, seat, 71, DeckStyle.Tempo);
            var other = options.FairViewFactory(variant, seat, 71, DeckStyle.Tempo);
            DriverTests.Check(Fingerprint(state) == Fingerprint(other), "Secret identity leaks into fair copy");
            DriverTests.Check(Trace(Decide(game, seat, options).Trace) == Trace(Decide(variant, seat, options).Trace), "Secret identity changes golden decision");
            var projectedEnemy = Copy(state).CurrentPlayer.Opponent;
            DriverTests.Check(!projectedEnemy.SecretZone.Any() && !Copy(state).IdEntityDic.Values.Any(c => c.Card.IsSecret),
                "Removed secret must retain neither a handler nor its private definition");
            DriverTests.Check(state.Facts.OpponentSecretCount == 1 && state.Evaluate(new CountEvaluator()) == 1001,
                "Evaluator missing public secret count or mana");
            DriverTests.Check(before.SequenceEqual(game.SaveSnapshot()), "Secret projection changed authoritative bytes");
            Entity.FromCard(bot, Cards.FromId("CS2_029"), zone: bot.HandZone);
            bot.Opponent.Hero.Damage = 24;
            var lethal = options.FairViewFactory(game, seat, 71, DeckStyle.Tempo);
            var hit = lethal.Actions.First(a => a.Type == PlayerTaskType.PLAY_CARD && a.Target == bot.Opponent.Hero.Id);
            DriverTests.Check(lethal.Apply(hit).Facts.Own.PlayState == PlayState.WON, "Removed Ice Block still triggers");
            Entity.FromCard(bot, Cards.FromId("EX1_295"), zone: bot.SecretZone);
            bool rejected = false;
            try { options.FairViewFactory(game, seat, 71, DeckStyle.Tempo); } catch (NotSupportedException) { rejected = true; }
            DriverTests.Check(rejected, "Golden options must reject own secrets");

            var deckGame = DriverTests.Fixture(seat, "CS2_182", "CS2_182", "CS2_182", "CS2_182", "CS2_182", "JAIL_384", "JAIL_421");
            var deckBot = deckGame.CurrentPlayer;
            var foreign = deckBot.DeckZone.AddGenerated(Cards.FromId("JAIL_421"), deckBot.Opponent.Hero);
            var deckVariant = HiddenVariant(deckGame, seat, false);
            var variantBot = deckVariant.CurrentPlayer;
            variantBot.DeckZone.ChangeEntity(deckVariant.IdEntityDic[foreign.Id], Entity.FromCard(variantBot, Cards.FromId("CS2_029"), id: foreign.Id));
            var golden = new FairOptions { TrackOwnDeck = true, MaterializeOwnDeckTriggers = true, RemoveOpponentSecrets = true };
            before = deckGame.SaveSnapshot();
            var known = FairBotState.Create(deckGame, seat, 83, DeckStyle.Tempo, golden);
            var twin = FairBotState.Create(deckVariant, seat, 83, DeckStyle.Tempo, golden);
            DriverTests.Check(known.Facts.OwnDeckKnowledge["JAIL_421"] == 2, "Hogger copy missing or enemy-generated Warptooth counted");
            DriverTests.Check(Fingerprint(known) == Fingerprint(twin), "Known-deck order or foreign card leaks");
            DriverTests.Check(before.SequenceEqual(deckGame.SaveSnapshot()), "Own-deck projection changed authoritative bytes");
            var ownCopy = Copy(known).CurrentPlayer;
            DriverTests.Check(ownCopy.DeckZone.Count(c => c.Card.Id == "JAIL_421") == 2
                && ownCopy.DeckZone.Count(c => c is Unknown) == 1,
                "Sampled multiset must contain both Warptooths and one unknown insertion");
            DriverTests.Check(Fingerprint(known) == Fingerprint(FairBotState.Create(deckGame, seat, 83, DeckStyle.Tempo, golden)),
                "Own-deck sampling must be deterministic for the decision seed");
            var fissure = Entity.FromCard(deckBot, Cards.FromId("CATA_582"), zone: deckBot.HandZone);
            for (int i = 0; i < 4; i++) Entity.FromCard(deckBot, Cards.FromId("CS2_182"), zone: deckBot.BoardZone);
            string deckBefore = Fingerprint(deckGame);
            known = FairBotState.Create(deckGame, seat, 83, DeckStyle.Tempo, golden);
            var damaged = known.Apply(known.Actions.First(a => a.Source == fissure.Id));
            DriverTests.Check(damaged.Facts.DistinctFriendlyCharactersDamagedThisTurn == 4
                && damaged.Facts.Own.Board.Count(c => c.CardId == "JAIL_421") == 2,
                "Fourth friendly damage must summon both canonical Warptooths");
            DriverTests.Check(!damaged.Facts.OwnDeckKnowledge.ContainsKey("JAIL_421"), "Summoned Warptooths remain in knowledge counts");
            DriverTests.Check(deckBefore == Fingerprint(deckGame), "Simulated Warptooth summon changed authority");
            var updatedVariant = HiddenVariant(deckGame, seat, false);
            var kdriver = new DriverOptions { FairViewFactory = (g, id, seed, style) => FairBotState.Create(g, id, seed, style, golden) };
            DriverTests.Check(Trace(Decide(deckGame, seat, kdriver).Trace) == Trace(Decide(updatedVariant, seat, kdriver).Trace), "Warptooth order changes decision");

            var replyGame = DriverTests.Fixture(seat);
            var replyBot = replyGame.CurrentPlayer;
            Entity.FromCard(replyBot, Cards.FromId("CS2_029"), zone: replyBot.HandZone);
            Entity.FromCard(replyBot.Opponent, Cards.FromId("CS2_182"), zone: replyBot.Opponent.HandZone);
            before = replyGame.SaveSnapshot();
            var root = FairBotState.Create(replyGame, seat, 93, DeckStyle.Tempo);
            var leaf = root.Apply(root.Actions.Single(a => a.Type == PlayerTaskType.END_TURN));
            var samples = Enumerable.Repeat("CS2_024", leaf.Facts.Opponent.Hand.Count).ToArray();
            var sampled = leaf.Determinize(samples, 100);
            DriverTests.Check(sampled.Facts.Opponent.Hand.All(c => c.CardId == "CS2_024")
                && Copy(sampled).CurrentPlayer.HandZone.Select(c => c.Id).SequenceEqual(Copy(leaf).CurrentPlayer.HandZone.Select(c => c.Id)), "Sample changed hand ids");
            DriverTests.Check(Fingerprint(sampled) == Fingerprint(leaf.Determinize(samples, 100)), "Determinization is not reproducible");
            var hiddenIds = new HashSet<int>(Copy(leaf).CurrentPlayer.HandZone.Select(c => c.Id));
            string PublicActors(FairBotState s) => string.Join("|", Copy(s).IdEntityDic.Values.Where(e => !hiddenIds.Contains(e.Id)).OrderBy(e => e.Id)
                .Select(e => e.Id + ":" + e.Card.Id + ":" + e.Zone?.Type + ":" + string.Join(",", e.NativeTags.OrderBy(t => (int)t.Key))));
            DriverTests.Check(PublicActors(sampled) == PublicActors(leaf), "Determinization changed non-hand actors");
            DriverTests.Check(Copy(sampled).HistoryFacts.CanonicalText() == Copy(leaf).HistoryFacts.CanonicalText(), "Sample acquired history");
            var reply = sampled.ForReply();
            DriverTests.Check(reply.Facts.Own.PlayerId == replyBot.Opponent.PlayerId && reply.CanExpand
                && reply.Facts.Own.Hand.All(c => c.CardId == "CS2_024") && reply.Facts.Opponent.Hand.All(c => c.Unknown), "Reply perspective leaks bot hand or loses samples");
            DriverTests.Check(reply.Facts.Turn == leaf.Facts.Turn && reply.Facts.Own.Mana == leaf.Facts.Opponent.Mana, "ForReply advanced turn twice");
            DriverTests.Check(before.SequenceEqual(replyGame.SaveSnapshot()) && leaf.Facts.Opponent.Hand.All(c => c.Unknown), "Determinization mutated authority or parent");
            DriverTests.Check(BotSearch.Choose(reply, BotBudget.ForStars(0).WithoutClock()).Nodes > 0, "Reply cannot search sampled hand");
            bool badLength = false, earlyReply = false;
            try { leaf.Determinize(new string[0], 1); } catch (ArgumentException) { badLength = true; }
            try { root.ForReply(); } catch (InvalidOperationException) { earlyReply = true; }
            DriverTests.Check(badLength && earlyReply, "Invalid determinization/reply boundary accepted");
        }
        Console.WriteLine("PASS fair view both seats: inert opponent secrets, own-secret rejection, evaluator facts, own origins/Hogger, canonical Warptooth, determinization/reply isolation");
    }
}
