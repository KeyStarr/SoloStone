using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static ReplyTests;

// Live 2026-10-26: a Discovered EDR_251 made every golden search fall back (TaskException wrapping the fair view's
// "Private historical origin" refusal). Search copies now carry fair origin beliefs, and boundaries unwrap the cause.
public static class HistorySearchTests
{
    private static Game Setup(int seat = 1) => Setup(seat, Options());
    private static FairOptions Options() => new FairOptions { RemoveOpponentSecrets = true, TrackOwnDeck = true, MaterializeOwnDeckTriggers = true };
    // Observation memory spans views, as GoldenPilot keeps it across one match's decisions.
    private static Game Setup(int seat, FairOptions observations)
    {
        var deck = Enumerable.Range(0, 30).Select(i => Cards.FromId(new[] { "CS2_182", "CS2_029", "CS2_120", "CS2_023" }[i % 4])).ToList();
        var game = new Game(new GameConfig { Player1HeroClass = CardClass.PALADIN, Player2HeroClass = CardClass.MAGE,
            Player1Deck = deck, Player2Deck = deck.ToList(), StartPlayer = seat, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false, RandomSeed = 7 });
        game.StartGame();
        foreach (var p in new[] { game.Player1, game.Player2 }) {
            foreach (var c in p.HandZone.ToArray()) { p.HandZone.Remove(c); p.SetasideZone.Add(c); }
            p.BaseMana = 10; p.UsedMana = 0;
        }
        // Public history on both sides: a seen foreign spell shuffled into each deck and a card played from hand.
        foreach (var p in new[] { game.Player1, game.Player2 }) {
            var generated = Generic.DrawCard(p, Cards.FromId("CS2_024"));
            if (p == game.CurrentPlayer) FairBotState.Create(game, p.PlayerId, 92, DeckStyle.Tempo, observations);
            p.HandZone.Remove(generated);
            Check(Generic.ShuffleIntoDeck(p, p.Hero, generated), "shuffle foreign spell");
        }
        foreach (var held in new[] { "CS2_182", "CS2_120" }) Entity.FromCard(game.CurrentPlayer.Opponent, Cards.FromId(held), zone: game.CurrentPlayer.Opponent.HandZone);
        var coin = Entity.FromCard(game.CurrentPlayer, Cards.FromId("GAME_005"), zone: game.CurrentPlayer.HandZone);
        Check(game.Process(game.CurrentPlayer.Options().First(t => t.Source?.Id == coin.Id)), "play a card from hand");
        return game;
    }
    private static FairBotState FairView(Game game, int seed = 71, FairOptions observations = null)
        => FairBotState.Create(game, game.CurrentPlayer.PlayerId, seed, DeckStyle.Tempo, observations ?? Options());
    private static GoldenDecision Decide(Game game)
    {
        var pilot = new GoldenPilot(NpcCatalog.All[0]);
        var root = pilot.Options.FairViewFactory(game, game.CurrentPlayer.PlayerId, 17037, DeckStyle.Tempo);
        // Called outside BotDriver's catch so a failure stays visible.
        return pilot.Decide(root, new GoldenContext(game.Turn, 0, 17037, pilot.Profile));
    }

    private static bool Partitioned(string[] drawn) => drawn.Length == 2 && drawn.Count(id => id == "CS2_024") == 1
        && drawn.Count(id => id == "CS2_023" || id == "CS2_029") == 1;
    public static void TestDragonscaleArmamentsDrawsBySampledOrigin()
    {
        foreach (int seat in new[] { 1, 2 }) {
            var observations = Options();
            var game = Setup(seat, observations); var bot = game.CurrentPlayer;
            var armaments = Entity.FromCard(bot, Cards.FromId("EDR_251"), zone: bot.HandZone);
            foreach (int seed in new[] { 71, 91, 113 }) {
                var view = FairView(game, seed, observations);
                var after = view.Apply(view.Actions.First(a => a.Source == armaments.Id));
                var drawn = after.Facts.Own.Hand.Select(c => c.CardId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
                // Original spells are Arcane Intellect/Fireball; the seen, shuffled Frostbolt is the only foreign one.
                Check(Partitioned(drawn), "fair draw partitions differ: " + string.Join(",", drawn));
            }
            var real = game.CloneExact();
            Check(real.Process(real.CurrentPlayer.Options().First(t => t.Source?.Id == armaments.Id)), "real play");
            Check(Partitioned(real.CurrentPlayer.HandZone.Select(c => c.Card.Id).OrderBy(id => id, StringComparer.Ordinal).ToArray()),
                "real game partitions changed");
        }
    }

    // Beliefs come from the bot's own observations and constants: hidden order, RNG and human cards cannot move them.
    public static void TestOriginBeliefsIgnoreHiddenState()
    {
        foreach (int seat in new[] { 1, 2 }) {
            var observations = Options();
            var game = Setup(seat, observations);
            var twin = game.CloneExact();
            twin.PowerHistory.Full.AddRange(game.PowerHistory.Full);
            var human = twin.CurrentPlayer.Opponent;
            twin.SetRandomSeed(991); twin.CurrentPlayer.DeckZone.Shuffle(); human.DeckZone.Shuffle();
            foreach (var c in human.DeckZone.ToArray()) human.DeckZone.ChangeEntity(c, Entity.FromCard(human, Cards.FromId("EX1_414"), id: c.Id));
            foreach (var c in human.HandZone.ToArray()) human.HandZone.ChangeEntity(c, Entity.FromCard(human, Cards.FromId("CS2_029"), id: c.Id));
            Func<Game, FairBotState> view = g => FairBotState.Create(g, g.CurrentPlayer.PlayerId, 71, DeckStyle.Tempo, new FairOptions {
                RemoveOpponentSecrets = true, TrackOwnDeck = true, MaterializeOwnDeckTriggers = true, Observations = observations.Observations });
            var left = view(game).FingerprintGame; var right = view(twin).FingerprintGame;
            Func<Game, string> deck = g => string.Join(",", g.CurrentPlayer.DeckZone.Select(c => c.Id + ":" + c.Card.Id + ":"
                + g.HistoryFacts.Query.KnownOrigin(c.Id, g.CurrentPlayer.Id)));
            Check(deck(left) == deck(right), "own-deck origin beliefs depend on hidden state");
            Check(left.HistoryFacts.CanonicalText() == right.HistoryFacts.CanonicalText(), "fair history depends on hidden state");
            Check(left.IdEntityDic.Values.All(e => left.HistoryFacts.Query.KnownOrigin(e.Id, e.Controller.Id) != null), "erased origin left in search copy");
        }
    }

    // TrackOwnDeck alone keeps own slots masked, but their origin beliefs still come from the ledger.
    public static void TestTrackedOwnDeckSlotsCarryLedgerOrigins()
    {
        foreach (bool materialize in new[] { false, true }) {
            var observations = Options();
            var game = Setup(1, observations);
            var view = FairBotState.Create(game, 1, 71, DeckStyle.Tempo, new FairOptions { RemoveOpponentSecrets = true,
                TrackOwnDeck = true, MaterializeOwnDeckTriggers = materialize, Observations = observations.Observations }).FingerprintGame;
            var deck = view.CurrentPlayer.DeckZone;
            Check(deck.All(c => c is Unknown) != materialize, "materialization mode differs");
            Check(deck.Count(c => view.HistoryFacts.Query.KnownOrigin(c.Id, view.CurrentPlayer.Id) == false) == 1,
                "exactly the seen shuffled Frostbolt is foreign (materialize=" + materialize + ")");
        }
    }

    public static void TestPilotSearchesNormallyHoldingHistoryCards()
    {
        var baseline = Decide(Setup());
        Check(string.IsNullOrEmpty(baseline.FallbackReason), "baseline fell back: " + baseline.FallbackReason);
        foreach (string id in new[] { "EDR_251", "TLC_513", "EDR_256", "BE_997" }) {
            var game = Setup(); var bot = game.CurrentPlayer;
            var held = Entity.FromCard(bot, Cards.FromId(id), zone: bot.HandZone);
            var decision = Decide(game);
            Check(string.IsNullOrEmpty(decision.FallbackReason), id + ": search fell back: " + decision.FallbackReason);
            Check(decision.Nodes > 20, id + ": search must expand normally, got " + decision.Nodes + " nodes");
            // Failing closed used to prune the live card's own line: the bot never considered playing it.
            if (id == "EDR_251") Check(decision.Candidates.Any(c => c.Key.Source == held.Id && !double.IsNegativeInfinity(c.Value)),
                id + ": playing the card was not searched");
        }
    }

    public static void TestDebugTaskWrappingStillReadsAsPrivateHistory()
    {
        var refusal = new NotSupportedException("Private historical origin is unavailable in a fair view.");
        // Debug engines (the shipped build) wrap task failures; live search then fell back on every decision.
        Check(FairHistory.IsPrivate(new SabberStoneCore.Exceptions.TaskException("task", refusal)), "wrapped refusal not recognised");
        Check(FairHistory.IsPrivate(refusal), "direct refusal not recognised");
        Check(!FairHistory.IsPrivate(new SabberStoneCore.Exceptions.TaskException("task", new NotSupportedException("other"))), "unrelated failure treated as private history");
    }

    // Rat Burglar reads which human hand cards arrived this turn; the projection erases those links, so its end-of-turn
    // branch stays private by design and search boundaries prune it (LethalTests, FairViewHiddenOfferProof).
    private static readonly HashSet<string> FailsClosed = new HashSet<string>(StringComparer.Ordinal) { "JAIL_205" };
    // Audit: every origin/hand-entry reader resolves in the bot's fair view and in a determinized reply sample.
    public static void TestHistoryReadersResolveInFairViews()
    {
        var readers = Cards.All.Where(c => c.Implemented && c.ConsumesHistory).Select(c => c.Id)
            .Concat(new[] { "EDR_256", "TLC_513", "TLC_513hp" }).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        Check(readers.Length > 50, "history reader census too small: " + readers.Length);
        var failures = new List<string>();
        foreach (string id in readers) {
            var card = Cards.FromId(id);
            if (card == null || card.Type == CardType.ENCHANTMENT || card.Type == CardType.INVALID) continue;
            try { Exercise(id, AuditSetup()); Exercise(id, Setup()); }
            catch (Exception error) when (FairHistory.IsPrivate(error) && !FailsClosed.Contains(id)) { failures.Add(id + ": " + error.GetBaseException().Message); }
            catch (Exception error) when (FairHistory.IsPrivate(error)) { }
        }
        Check(failures.Count == 0, "private history reached in fair search:\n" + string.Join("\n", failures));
    }
    // The human moved first: a face-down Secret and a visible play leave definition-private opponent history.
    private static Game AuditSetup()
    {
        var game = Setup(2); var human = game.CurrentPlayer;
        var secret = Entity.FromCard(human, Cards.All.Where(c => c.Implemented && c.Collectible && c.IsSecret && c.Class == CardClass.MAGE)
            .OrderBy(c => c.Id, StringComparer.Ordinal).First(), zone: human.HandZone);
        var minion = Entity.FromCard(human, Cards.FromId("CS2_120"), zone: human.HandZone);
        foreach (var played in new IPlayable[] { secret, minion })
            Check(game.Process(human.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == played.Id)), "human play");
        Check(game.Process(EndTurnTask.Any(human)), "human ends turn");
        foreach (var p in new[] { game.Player1, game.Player2 }) { p.BaseMana = 10; p.UsedMana = 0; }
        return game;
    }
    private static void Exercise(string id, Game game)
    {
        var bot = game.CurrentPlayer;
        var card = Cards.FromId(id);
        IPlayable actor;
        if (card.Type == CardType.HERO_POWER) {
            new SabberStoneCore.Tasks.SimpleTasks.ReplaceHeroPower(card).Process(game, bot, bot.Hero, null);
            actor = bot.Hero.HeroPower;
        }
        else actor = Entity.FromCard(bot, card, zone: bot.HandZone);
        if (card.Type == CardType.MINION && !bot.BoardZone.IsFull)
            Entity.FromCard(bot, card, zone: bot.BoardZone);
        var view = FairView(game);
        foreach (var action in view.Actions.Where(a => a.Source == actor.Id || a.Type == PlayerTaskType.END_TURN).Take(6)) {
            FairBotState after;
            try { after = view.Apply(action); }
            catch (Exception error) when (!FairHistory.IsPrivate(error)) { continue; }
            // Settle offers with the engine order, then hand the turn over as the reply model does.
            for (int i = 0; i < 3 && after.HasPendingChoice && after.CanExpand && after.OrderedActions.Any(); i++) after = after.Apply(after.OrderedActions.First());
            if (action.Type != PlayerTaskType.END_TURN && !after.HasPendingChoice) {
                try { after = after.ForReplyEndTurn(); }
                catch (Exception error) when (!FairHistory.IsPrivate(error)) { }
            }
        }
        // The simulated human holds the card too: its reply view must resolve on the bot's beliefs.
        var ended = view.ForReplyEndTurn();
        int unknown = ended.Facts.Opponent.Hand.Count(c => c.Unknown);
        var sample = Enumerable.Repeat("CS2_182", unknown).ToList();
        if (unknown > 0 && card.Type != CardType.HERO_POWER) sample[unknown - 1] = id;
        var reply = ended.Determinize(sample, 5).ForReply();
        foreach (var action in reply.Actions.Take(8)) {
            try { reply.Apply(action); }
            catch (Exception error) when (!FairHistory.IsPrivate(error)) { }
        }
    }
}
