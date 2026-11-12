using System;
using System.Linq;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static ReplyTests;

public static class OwnDeckTests
{
    private static Game Setup(int seat, string target)
    {
        var deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 10)
            .Concat(Enumerable.Repeat(Cards.FromId(target), 10))
            .Concat(Enumerable.Repeat(Cards.FromId("CS2_120"), 10)).ToList();
        var game = new Game(new GameConfig { Player1Deck = deck, Player2Deck = deck.ToList(),
            Player1HeroClass = CardClass.SHAMAN, Player2HeroClass = CardClass.PALADIN,
            StartPlayer = seat, Shuffle = false, SkipMulligan = true, History = true, RandomSeed = 7 });
        game.StartGame();
        foreach (var p in new[] { game.Player1, game.Player2 }) {
            foreach (var c in p.HandZone.ToArray()) { p.HandZone.Remove(c); p.SetasideZone.Add(c); }
            p.BaseMana = 10; p.UsedMana = 0;
        }
        return game;
    }
    private static FairBotState View(Game game, int seat, int seed = 71) => FairBotState.Create(game, seat, seed,
        DeckStyle.Tempo, new FairOptions { RemoveOpponentSecrets = true, TrackOwnDeck = true, MaterializeOwnDeckTriggers = true });
    public static void TestOwnDeckSamplesIgnoreRealOrderAndHumanCardsBothSeats()
    {
        foreach (int seat in new[] { 1, 2 }) {
            var game = Setup(seat, "CATA_565"); var bot = game.CurrentPlayer;
            var first = View(game, seat); var changed = game.CloneExact();
            // Exact clones drop presentation history; a hidden twin must retain the same observations.
            changed.PowerHistory.Full.AddRange(game.PowerHistory.Full);
            var other = changed.CurrentPlayer;
            changed.SetRandomSeed(991); other.DeckZone.Shuffle(); other.Opponent.DeckZone.Shuffle();
            other.DeckCards.Reverse();
            foreach (var c in other.Opponent.DeckZone.ToArray())
                other.Opponent.DeckZone.ChangeEntity(c, Entity.FromCard(other.Opponent, Cards.FromId("EX1_414"), id: c.Id));
            Entity.FromCard(other.Opponent, Cards.FromId("CS2_029"), zone: other.Opponent.HandZone);
            Entity.FromCard(bot.Opponent, Cards.FromId("CS2_182"), zone: bot.Opponent.HandZone);
            first = View(game, seat); var second = View(changed, seat);
            Func<FairBotState, string> order = s => string.Join(",", s.FingerprintGame.CurrentPlayer.DeckZone.Select(c => c.Id + ":" + c.Card.Id));
            Check(order(first) == order(second), "sample depends on live order, RNG or human identities");
            Check(first.FingerprintGame.CurrentPlayer.Opponent.DeckZone.All(c => c is Unknown)
                && first.Facts.Opponent.Hand.All(c => c.Unknown), "human hidden cards escaped masking");
            var expected = bot.DeckZone.GroupBy(c => c.Card.Id).ToDictionary(g => g.Key, g => g.Count());
            foreach (int seed in new[] { 71, 91, 113 }) {
                var sample = View(game, seat, seed).FingerprintGame.CurrentPlayer;
                Check(sample.DeckZone.All(c => !(c is Unknown)), "known own deck stayed masked");
                Check(sample.DeckZone.GroupBy(c => c.Card.Id).All(g => expected[g.Key] == g.Count()), "sample changed own multiset");
            }
            Check(order(first) != order(View(game, seat, 91)), "artificial seed must change own sample order");
            // Snapshot equality is tested on a fresh root, without fixture mutations.
            var pristine = Setup(seat, "CATA_565"); var before = pristine.SaveSnapshot(); View(pristine, seat);
            Check(before.SequenceEqual(pristine.SaveSnapshot()), "projection mutated authoritative state");
        }
    }
    public static void TestEggbearerDrawsKnownTargetsIncludingDoubledBattlecry()
    {
        foreach (int seat in new[] { 1, 2 }) foreach (bool doubled in new[] { false, true }) {
            var game = Setup(seat, "CATA_565"); var bot = game.CurrentPlayer;
            if (doubled) {
                new ReplaceHeroPower(Cards.FromId("JAIL_800hp2")).Process(game, bot, bot.Hero, null);
                for (int i = 0; i < 4; i++) {
                    var filler = Entity.FromCard(bot, Cards.FromId("CS2_231"), zone: bot.HandZone); filler.Cost = 0;
                    Check(game.Process(bot.Options().First(t => t.Source?.Id == filler.Id)), "fifth setup");
                }
            }
            var egg = Entity.FromCard(bot, Cards.FromId("DINO_411"), zone: bot.HandZone);
            var view = View(game, seat); var after = view.Apply(view.Actions.First(a => a.Source == egg.Id));
            int count = doubled ? 2 : 1;
            Check(after.Facts.Own.Hand.Count == count && after.Facts.Own.Hand.All(c => c.CardId == "CATA_565"), "Eggbearer must draw known zero-attack targets");
            Check(after.Facts.OwnDeckKnowledge["CATA_565"] == view.Facts.OwnDeckKnowledge["CATA_565"] - count, "draw must consume own knowledge");
            Check(game.Process(bot.Options().First(t => t.Source?.Id == egg.Id)) && bot.HandZone.Count == count, "fair/engine tutor count differs");
        }
    }
    public static void TestOrdinaryDrawConsumesOnlyRemainingOwnMultiset()
    {
        foreach (int seat in new[] { 1, 2 }) {
            var game = Setup(seat, "CATA_565"); var bot = game.CurrentPlayer;
            var draw = Entity.FromCard(bot, Cards.FromId("CS2_023"), zone: bot.HandZone);
            var view = View(game, seat); var after = view.Apply(view.Actions.First(a => a.Source == draw.Id));
            Check(after.Facts.Own.Hand.Count == 2 && after.Facts.Own.Hand.All(c => !c.Unknown), "ordinary draws need sampled definitions");
            foreach (var group in after.Facts.Own.Hand.GroupBy(c => c.CardId)) {
                Check(view.Facts.OwnDeckKnowledge.ContainsKey(group.Key), "draw came from outside remaining own multiset");
                int left; after.Facts.OwnDeckKnowledge.TryGetValue(group.Key, out left);
                Check(left == view.Facts.OwnDeckKnowledge[group.Key] - group.Count(), "sampled draws failed to consume known copies");
            }
        }
    }
    public static void TestGelbinSeesKnownDeckAuras()
    {
        foreach (int seat in new[] { 1, 2 }) {
            var game = Setup(seat, "TIME_009t1"); var bot = game.CurrentPlayer;
            var gelbin = Entity.FromCard(bot, Cards.FromId("TIME_009"), zone: bot.HandZone);
            var view = View(game, seat); var after = view.Apply(view.Actions.First(a => a.Source == gelbin.Id));
            Check(after.FingerprintGame.CurrentPlayer.SecretZone.Auras.Any(c => c.Card.Id == "TIME_009t1"), "Gelbin failed to deploy known aura");
        }
    }
}
