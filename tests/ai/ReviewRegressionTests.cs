using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class ReviewRegressionTests
{
    private static readonly FairOptions Golden = new FairOptions {
        TrackOwnDeck = true, MaterializeOwnDeckTriggers = true, RemoveOpponentSecrets = true };
    private static FairBotState View(Game game) => FairBotState.Create(game, 1, 71, DeckStyle.Tempo, Golden);
    private static void Play(Game game, string id)
    {
        var card = Generic.DrawCard(game.CurrentPlayer, Cards.FromId(id));
        DriverTests.Check(game.Process(game.CurrentPlayer.Options().First(t => t.Source?.Id == card.Id)), "Fixture play rejected");
    }
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (Action test in new Action[] { DeckOrder, HiddenIds, TagOrder, HeadlessTasks, RewindCache,
            DuplicateActions, ReplyTriggers, RewindSecrets, PlayedSecretHistory })
        {
            try { test(); Console.WriteLine("PASS review regression " + test.Method.Name); }
            catch (Exception error) { failures.Add(test.Method.Name + ": " + error.Message); }
        }
        DriverTests.Check(failures.Count == 0, string.Join("\n", failures));
    }
    private static void DeckOrder()
    {
        var game = DriverTests.Fixture(1, "CS2_182", "CS2_182", "CS2_182", "CS2_182", "JAIL_421", "CS2_029", "CS2_120");
        var variant = game.CloneExact();
        variant.SetRandomSeed(8427); variant.Player1.DeckZone.Shuffle();
        var first = View(game); var second = View(variant);
        DriverTests.Check(first.Facts.OwnDeckKnowledge.Count >= 3, "Need distinct own-deck identities");
        DriverTests.Check(FairViewTests.Fingerprint(first) == FairViewTests.Fingerprint(second), "Own deck order leaks through facts");
        foreach (var state in new[] { first, first.Apply(first.Actions.Single(a => a.Type == PlayerTaskType.END_TURN)) })
            DriverTests.Check(state.Facts.OwnDeckKnowledge.Keys.SequenceEqual(state.Facts.OwnDeckKnowledge.Keys.OrderBy(k => k, StringComparer.Ordinal)),
                "Own-deck knowledge must enumerate ordinal keys at root and child");
    }
    private static void HiddenIds()
    {
        var game = DriverTests.Fixture(); game.Player1.UsedMana = 10;
        Entity.FromCard(game.Player2, Cards.FromId("CS2_029"), zone: game.Player2.HandZone);
        Entity.FromCard(game.Player2, Cards.FromId("CS2_120"), zone: game.Player2.HandZone);
        var variant = game.CloneExact();
        var hand = variant.Player2.HandZone.ToArray();
        foreach (var card in hand) variant.Player2.HandZone.Remove(card);
        foreach (var card in hand.Reverse()) variant.Player2.HandZone.Add(card);
        var first = View(game); var second = View(variant);
        DriverTests.Check(first.Facts.Opponent.Hand.All(c => c.Unknown && c.EntityId == 0), "Hidden entity ids expose decklist slots");
        DriverTests.Check(FairViewTests.FactsFingerprint(first) == FairViewTests.FactsFingerprint(second), "Permuted hidden ids change facts");
        DriverTests.Check(DriverTests.Trace(DriverTests.Decide(game, 1).Trace) == DriverTests.Trace(DriverTests.Decide(variant, 1).Trace), "Permuted hidden ids change trace");
    }
    private static void TagOrder()
    {
        var game = DriverTests.Fixture(); var variant = game.CloneExact();
        var tags = variant.Player1.Hero.NativeTags.OrderByDescending(t => (int)t.Key).ToArray();
        variant.Player1.Hero.NativeTags.Clear();
        foreach (var tag in tags) variant.Player1.Hero.NativeTags.Add(tag.Key, tag.Value);
        var state = View(game);
        DriverTests.Check(FairViewTests.Fingerprint(state) == FairViewTests.Fingerprint(View(variant)), "Tag insertion order leaks through facts");
        DriverTests.Check(state.Facts.Own.Hero.Tags.Keys.SequenceEqual(state.Facts.Own.Hero.Tags.Keys.OrderBy(t => t)), "Tags must enumerate by GameTag");
    }
    private static void HeadlessTasks()
    {
        foreach (bool fallback in new[] { false, true })
        {
            var game = DriverTests.Fixture(); game.Player1.UsedMana = 10;
            var location = (BoardObject)Entity.FromCard(game.Player1, Cards.FromId("CORE_REV_990"), zone: game.Player1.BoardZone);
            var victim = Entity.FromCard(game.Player2, Cards.FromId("CS2_231"), zone: game.Player2.BoardZone);
            game.DeathProcessingAndAuraUpdate();
            var before = game.SaveSnapshot(); int index = 0;
            var result = BotDriver.Decide(game, 1, DeckStyle.Tempo, state => fallback
                ? throw new InvalidOperationException("forced regression fallback")
                : new PilotDecision { Action = state.Actions.Single(a => a.Type == PlayerTaskType.ACTIVATE_LOCATION) }, ref index);
            DriverTests.Check(result.Trace.Kind == (fallback ? "fallback" : "search"), "Wrong driver path");
            DriverTests.Check(before.SequenceEqual(game.SaveSnapshot()), "Headless options mutate authoritative bytes");
            DriverTests.Check(result.Task.PlayerTaskType == PlayerTaskType.ACTIVATE_LOCATION && game.Process(result.Task), "Headless activation belongs to another game");
            DriverTests.Check(location.Durability == 2 && victim.Zone == game.Player2.GraveyardZone, "Headless activation did not affect authority");
        }
    }
    private static void RewindCache()
    {
        var game = DriverTests.Fixture(); Play(game, "TIME_034");
        var tasks = game.Player1.Options();
        var field = typeof(Controller).GetField("_opponent", BindingFlags.Instance | BindingFlags.NonPublic);
        field.SetValue(game.Player1, null); field.SetValue(game.Player2, null);
        var before = game.SaveSnapshot();
        int index = 0;
        DriverTests.Check(BotDriver.Decide(game, 1, DeckStyle.Tempo, _ => throw new Exception("Unexpected search"),
            ref index, new DriverOptions { Rewind = view => true }, tasks).Trace.Kind == "rewind", "Rewind hook not called");
        DriverTests.Check(before.SequenceEqual(game.SaveSnapshot()), "Rewind hook warms authoritative opponent cache");
    }
    private static void DuplicateActions()
    {
        var game = DriverTests.Fixture(); Play(game, "EDR_209");
        game.Player1.Choice.Choices.Add(game.Player1.Choice.Choices[0]);
        var state = View(game);
        DriverTests.Check(state.OrderedActions.Length > state.OrderedActions.Distinct().Count(), "Fixture lacks duplicate actions");
        var decision = BotSearch.Choose(state, new BotBudget(0, 10, 100, int.MaxValue));
        DriverTests.Check(decision.Candidates.Count == state.OrderedActions.Distinct().Count(), "Duplicate candidate actions retained");
    }
    private static void ReplyTriggers()
    {
        var game = DriverTests.Fixture(1, "CS2_182", "CS2_182", "CS2_182", "CS2_182", "JAIL_421");
        var root = View(game);
        var leaf = root.Apply(root.Actions.Single(a => a.Type == PlayerTaskType.END_TURN));
        var reply = leaf.Determinize(Enumerable.Repeat("CS2_120", leaf.Facts.Opponent.Hand.Count).ToArray(), 3).ForReply();
        DriverTests.Check(FairViewTests.Copy(reply).Player1.DeckZone.Count(c => c.Card.Id == "JAIL_421") == 1,
            "Reply discarded canonical bot Warptooth");
    }
    private static void RewindSecrets()
    {
        var game = DriverTests.Fixture(); Play(game, "EX1_295"); game.Player1.UsedMana = 0; Play(game, "TIME_034");
        var state = View(game);
        DriverTests.Check(state.Actions.Length == 1 && state.Actions[0].Type == PlayerTaskType.CHOOSE, "Own secret blocks pending Rewind keep");
    }
    private static void PlayedSecretHistory()
    {
        string fingerprint = null, trace = null;
        foreach (string id in new[] { "EX1_295", "EX1_287" })
        {
            var game = DriverTests.Fixture(2); Play(game, id); DriverTests.Check(game.Process(EndTurnTask.Any(game.Player2)), "Secret setup end turn");
            var state = View(game);
            var decision = DriverTests.Decide(game, 1, new DriverOptions { FairViewFactory = (g, owner, seed, style) => FairBotState.Create(g, owner, seed, style, Golden) });
            DriverTests.Check(fingerprint == null || fingerprint == FairViewTests.Fingerprint(state), "Played secret history reveals identity");
            DriverTests.Check(trace == null || trace == DriverTests.Trace(decision.Trace), "Played secret history changes trace");
            fingerprint = FairViewTests.Fingerprint(state); trace = DriverTests.Trace(decision.Trace);
        }
    }
}
