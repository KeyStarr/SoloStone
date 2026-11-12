using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class DriverTests
{
    internal static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    internal static Game Fixture(int seat = 1, params string[] deckPrefix)
    {
        var deck = deckPrefix.Concat(Enumerable.Repeat("CS2_182", 30 - deckPrefix.Length)).Select(id => Cards.FromId(id)).ToList();
        var game = new Game(new GameConfig { Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.MAGE,
            Player1Deck = deck, Player2Deck = deck.ToList(), StartPlayer = seat, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false, RandomSeed = 7 });
        game.StartGame();
        foreach (var player in new[] { game.Player1, game.Player2 })
        {
            foreach (var card in player.HandZone.ToArray()) { player.HandZone.Remove(card); player.SetasideZone.Add(card); }
            player.BaseMana = 10; player.UsedMana = 0;
        }
        return game;
    }
    internal static Game HiddenVariant(Game game, int owner, bool varyOwnDeck = true)
    {
        var copy = game.CloneExact();
        var bot = copy.Player1.PlayerId == owner ? copy.Player1 : copy.Player2;
        foreach (var old in bot.Opponent.HandZone.ToArray())
            bot.Opponent.HandZone.ChangeEntity(old, Entity.FromCard(bot.Opponent, Cards.FromId("CS2_029"), id: old.Id));
        foreach (var old in bot.Opponent.DeckZone.ToArray())
            bot.Opponent.DeckZone.ChangeEntity(old, Entity.FromCard(bot.Opponent, Cards.FromId("EX1_414"), id: old.Id));
        if (varyOwnDeck)
            foreach (var old in bot.DeckZone.ToArray())
                bot.DeckZone.ChangeEntity(old, Entity.FromCard(bot, Cards.FromId("CS2_200"), id: old.Id));
        copy.SetRandomSeed(789123); bot.DeckZone.Shuffle(); bot.Opponent.DeckZone.Shuffle();
        return copy;
    }
    internal static string Trace(DriverTrace trace) => trace.Kind + "|" + trace.FallbackReason + "|" + trace.PilotDecision.Action
        + "|" + trace.PilotDecision.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
        + "|" + trace.PilotDecision.Nodes + "|" + trace.PilotDecision.Depth + "|" + trace.PilotDecision.Stop
        + "|" + string.Join(";", trace.PilotDecision.Candidates.Select(c => c.Key + "=" + c.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
    internal static (PlayerTask Task, DriverTrace Trace) Decide(Game game, int owner, DriverOptions options = null)
    {
        int index = 0;
        return BotDriver.Decide(game, owner, DeckStyle.Tempo,
            state => PilotDecision.From(BotSearch.Choose(state, BotBudget.ForStars(0).WithoutClock())), ref index, options);
    }
    internal static void Parity(Game game, int owner)
    {
        game = game.CloneExact();
        var bot = game.Player1.PlayerId == owner ? game.Player1 : game.Player2;
        var tasks = bot.Options();
        PlayerTask expected = LocalBot.RewindKeep(tasks);
        if (expected == null)
        {
            try { expected = BotSearch.Choose(FairBotState.Create(game, owner, unchecked(997 * game.Turn + 31), DeckStyle.Tempo),
                BotBudget.ForStars(0).WithoutClock()).Action.Resolve(tasks); }
            // Safe recovery deliberately passes when secrets prevent constructing any fair state.
            catch (NotSupportedException error) when (error.Message.StartsWith("Fair search does not model secrets"))
            { expected = tasks.Single(t => t.PlayerTaskType == PlayerTaskType.END_TURN); }
            catch { expected = LocalBot.Choose(tasks); }
        }
        var actual = Decide(game, owner);
        Check(BotAction.From(expected).Equals(BotAction.From(actual.Task)), "Driver differs from pre-refactor inline policy");
        Check(Trace(actual.Trace) == Trace(Decide(game, owner, new DriverOptions()).Trace), "Null driver options change behavior");
    }
    internal static void Pair(Game game, int owner, string expectedKind, DriverOptions options = null)
    {
        var before = game.SaveSnapshot();
        var result = Decide(game, owner, options);
        Check(result.Trace.Kind == expectedKind, "Expected driver path " + expectedKind + ", got " + Trace(result.Trace));
        Check(Trace(result.Trace) == Trace(Decide(HiddenVariant(game, owner), owner, options).Trace), "Hidden variant changed " + expectedKind);
        Check(before.SequenceEqual(game.SaveSnapshot()), "Driver changed authoritative bytes");
        Parity(game, owner);
    }
    internal static void ValuesOnly(System.Type type, HashSet<System.Type> seen = null)
    {
        seen ??= new HashSet<System.Type>();
        if (!seen.Add(type) || type.IsPrimitive || type.IsEnum || type == typeof(string)) return;
        Check(type.Namespace == null || !type.Namespace.StartsWith("SabberStoneCore"), "Engine reference in value view: " + type);
        if (type.IsArray) { ValuesOnly(type.GetElementType(), seen); return; }
        if (type.IsGenericType) foreach (var arg in type.GetGenericArguments()) ValuesOnly(arg, seen);
        if (type.Assembly == typeof(DriverTests).Assembly)
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) ValuesOnly(field.FieldType, seen);
    }
    internal static void Run()
    {
        ReviewRegressionTests.Run();
        var offered = new[] { new MulliganCard(10, "CS2_182", 4), new MulliganCard(11, "GAME_005", 0) };
        var view = new MulliganView(offered, true, new[] { "CS2_182" }, CardClass.MAGE);
        Check(BotDriver.Mulligan(view, MulliganPolicy.ReplaceAll).Count == 0, "ReplaceAll must keep nothing");
        Check(BotDriver.Mulligan(view, MulliganPolicy.KeepAll).SequenceEqual(new[] { 10 }), "KeepAll must exclude Coin");
        ValuesOnly(typeof(MulliganView)); ValuesOnly(typeof(RewindView)); ValuesOnly(typeof(FairFacts));
        foreach (int seat in new[] { 1, 2 })
        {
            var game = Fixture(seat); var bot = game.CurrentPlayer;
            Entity.FromCard(bot, Cards.FromId("CS2_029"), zone: bot.HandZone);
            Entity.FromCard(bot.Opponent, Cards.FromId("CS2_182"), zone: bot.Opponent.HandZone);
            Pair(game, seat, "search");
            Entity.FromCard(bot.Opponent, Cards.FromId("EX1_295"), zone: bot.Opponent.SecretZone);
            Pair(game, seat, "fallback");
            Check(Decide(game, seat).Trace.FallbackReason.StartsWith("NotSupportedException:Fair search does not model secrets"), "Secret fallback reason missing");
        }
        Console.WriteLine("PASS driver parity, hidden variants on both seats, secret fallback, value-only mulligans");
        DriverFairnessTests.Run();
    }
}
