using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;

internal static class EffectFrameProof
{
    private static Util.DeepCloneableRandom Random(Game game) => (Util.DeepCloneableRandom)typeof(Game)
        .GetProperty("Random", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception("effects: " + message);
    }

    // Test-only task definitions; no production card scripts are changed.
    private sealed class Before : SimpleTask
    {
        public override TaskState Process(in Game game, in Controller controller, in IEntity source,
            in IPlayable target, in TaskStack stack = null)
        {
            controller.Opponent.Hero.Damage += 2;
            stack.Number = Random(game).Next(1000);
            controller.Hero.Armor = stack.Number;
            stack.Number1 = 11; stack.Number2 = 22; stack.Number3 = 33; stack.Number4 = 44; stack.Flag = true;
            return TaskState.COMPLETE;
        }
    }
    private sealed class Apply : SimpleTask
    {
        public override TaskState Process(in Game game, in Controller controller, in IEntity source,
            in IPlayable target, in TaskStack stack = null)
        {
            Check(stack.Flag && stack.Number == controller.Hero.Armor && stack.Number1 == 11 && stack.Number2 == 22 &&
                stack.Number3 == 33 && stack.Number4 == 44, "stack scalar capture");
            Check(stack.Playables.Count == 2, "both choices propagated through nested frames");
            foreach (var entity in stack.Playables)
            {
                Check(entity.Game == game, "chosen entity rebound to destination game");
                Generic.RemoveFromZone(controller, entity);
                Generic.AddHandPhase(controller, entity);
            }
            controller.Opponent.Hero.Damage += 3;
            return TaskState.COMPLETE;
        }
    }

    private static readonly Lazy<SuspendableTask> Recipe = new(() => StateTaskList.Suspendable("proof/two-discovers/v1",
        new Before(), new DiscoverThenContinueTask(CardType.MINION),
        StateTaskList.Suspendable("proof/nested-discover/v1", new DiscoverThenContinueTask(CardType.SPELL)), new Apply()));

    private static readonly Lazy<SuspendableTask> SequentialRecipe = new(() => StateTaskList.Suspendable("proof/sequential-discovers/v1",
        new Before(), new DiscoverThenContinueTask(CardType.MINION), new DiscoverThenContinueTask(CardType.SPELL), new Apply()));
    private static readonly Dictionary<string, Card> TestCards = new();
    private static void Register()
    {
        if (TestCards.Count != 0) return;
        var data = typeof(Cards).GetField("Data", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var cards = (Dictionary<string, Card>)data.GetType().GetProperty("Cards", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(data);
        foreach (var recipe in new[] { Recipe.Value, SequentialRecipe.Value })
        {
            var card = Cards.FromId("GAME_005").Clone();
            typeof(Card).GetProperty("Id").SetValue(card, "TEST_10B_" + TestCards.Count);
            typeof(Card).GetField("<AssetId>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(card, -10000 - TestCards.Count);
            card.Tags[GameTag.COLLECTIBLE] = 0;
            card.Power = new Power { PowerTask = recipe };
            cards.Add(card.Id, card);
            TestCards.Add(recipe.RecipeKey, card);
        }
    }

    private static Game Fixture(bool nested = true)
    {
        var game = new Game(new GameConfig { RandomSeed = 91, StartPlayer = 1, SkipMulligan = true,
            Player1HeroClass = CardClass.DRUID, History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(), FormatType = FormatType.FT_WILD });
        game.StartGame();
        Register();
        var recipe = nested ? Recipe.Value : SequentialRecipe.Value;
        var card = Entity.FromCard(game.Player1, TestCards[recipe.RecipeKey], zone: game.Player1.HandZone);
        Check(!card.Card.Collectible && game.Process(PlayCardTask.Any(game.Player1, card)), "synthetic noncollectible spell play");
        return game;
    }
    private static string State(Game game) => Canonical.State(game.CloneExact());
    private static void Same(Game game, Game copy, bool history)
    {
        Check(State(game) == State(copy), "state diverged");
        Check(Random(game).CaptureState().Equals(Random(copy).CaptureState()), "RNG diverged");
        Check(game.GetPendingDecisions().Select(v => v.ToJson()).SequenceEqual(copy.GetPendingDecisions().Select(v => v.ToJson())), "offers diverged");
        if (history) Check(Canonical.History(game) == Canonical.History(copy), "history diverged");
    }
    private static void Reject(Game game, DecisionAnswer answer)
    {
        var bytes = game.SaveSnapshot();
        Check(!game.TryAnswer(answer, out _), "invalid answer accepted");
        Check(bytes.SequenceEqual(game.SaveSnapshot()), "rejected answer mutated saved state/history/RNG");
    }
    private static DecisionAnswer Answer(Game game)
    {
        var view = game.GetPendingDecisions().Single();
        return new DecisionAnswer(view.Id, view.ControllerId, new[] { view.Options[0].OptionId });
    }
    private static void Accept(Game game, DecisionAnswer answer)
        => Check(game.TryAnswer(answer, out var error), "valid answer rejected: " + error);

    private static void Branches(bool nested, int first, int second)
    {
        var game = Fixture(nested);
        int hand = game.Player1.HandZone.Count;
        var chosen = new List<int>();
        Check(game.Player2.Hero.Damage == 2, "initial work once, final work waits");
        for (int stage = 0; stage < 2; stage++)
        {
            var view = game.GetPendingDecisions().Single();
            Check(view.Kind == DecisionKind.SuspendedEffect, "suspended frame exposed");
            var bytes = game.SaveSnapshot();
            var restored = Game.LoadSnapshot(bytes);
            var clone = game.CloneExact(history: true);
            Check(bytes.SequenceEqual(restored.SaveSnapshot()), "frame snapshot byte stability");
            Same(game, restored, true); Same(game, clone, false);
            Check(bytes.SequenceEqual(game.SaveSnapshot()), "snapshot/clone changed source");
            Reject(game, new DecisionAnswer(view.Id, game.Player2.Id, new[] { view.Options[0].OptionId }));
            Reject(game, new DecisionAnswer(view.Id, view.ControllerId, Array.Empty<int>()));
            Reject(game, new DecisionAnswer(view.Id, view.ControllerId, new[] { int.MaxValue }));
            Reject(game, new DecisionAnswer(view.Id, view.ControllerId, new[] { view.Options[0].OptionId, view.Options[0].OptionId }));
            Check(!game.Process(EndTurnTask.Any(game.Player1)), "normal action accepted while suspended");
            Check(bytes.SequenceEqual(game.SaveSnapshot()), "rejected normal action mutated game");
            Check(!game.Process(ChooseTask.Pick(game.Player1, view.Options[0].OptionId)), "legacy choice bypass accepted");
            Check(bytes.SequenceEqual(game.SaveSnapshot()), "legacy choice bypass mutated game");
            var rng = Random(game).CaptureState();
            int pick = view.Options[stage == 0 ? first : second].OptionId;
            chosen.Add(pick);
            var answer = new DecisionAnswer(view.Id, view.ControllerId, new[] { pick });
            var phases = new List<ResolutionKind>();
            game.Resolution.Trace = observation => { if (observation.Stage == "start") phases.Add(observation.Frame.Kind); };
            foreach (var candidate in new[] { game, restored, clone }) Accept(candidate, answer);
            game.Resolution.Trace = null;
            if (stage == 0) Check(!phases.Contains(ResolutionKind.Cleanup), "nested suspension ran cleanup before effect completed");
            Same(game, restored, true); Same(game, clone, false);
            if (stage == 1) Check(Random(game).CaptureState().Equals(rng), "final resume consumed fresh RNG");
            Check(Canonical.Value(game.PowerHistory.Last) == Canonical.Value(clone.PowerHistory.Last), "clone resume history suffix");
            Reject(game, answer);
            Check(game.Player2.Hero.Damage == (stage == 0 ? 2 : 5), "earlier damage replayed or final work missing");
            Check(game.Player1.HandZone.Count == hand + (stage == 0 ? 0 : 2), "chosen cards applied exactly once");
        }
        Check(chosen.All(id => game.Player1.HandZone.Any(c => c.Id == id)), "selected entities applied, not substitute cards");
        Check(!game.Resolution.HasSuspendedEffect, "completed frame retained");
        Check(game.Resolution.Depth == 0 && game.Resolution.FramesStarted == game.Resolution.FramesEnded, "unbalanced scheduler frames");
        Check(game.Process(EndTurnTask.Any(game.Player1)), "normal action still blocked after resume");
    }

    private static string Future(Game game)
    {
        while (game.Resolution.HasSuspendedEffect) Accept(game, Answer(game));
        return Canonical.Sha(State(game) + Canonical.History(game) + Canonical.Value(Random(game).CaptureState()));
    }

    internal static void Child(string directory)
    {
        Register();
        foreach (var file in Directory.GetFiles(directory, "*.snapshot"))
        {
            var bytes = File.ReadAllBytes(file);
            var game = Game.LoadSnapshot(bytes);
            Check(bytes.SequenceEqual(game.SaveSnapshot()), "fresh process frame bytes");
            Check(Future(game) == File.ReadAllText(file + ".hash"), "fresh process continuation differs");
        }
        Console.WriteLine("PASS suspended effect fresh-process restore");
    }

    private static void FreshProcess()
    {
        string directory = Path.Combine(Path.GetTempPath(), "hs-effects-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var game = Fixture();
            for (int stage = 0; stage < 2; stage++)
            {
                var file = Path.Combine(directory, stage + ".snapshot");
                var bytes = game.SaveSnapshot();
                File.WriteAllBytes(file, bytes);
                File.WriteAllText(file + ".hash", Future(Game.LoadSnapshot(bytes)));
                Accept(game, Answer(game));
            }
            var start = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath) { RedirectStandardOutput = true, RedirectStandardError = true };
            if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("--effects-child"); start.ArgumentList.Add(directory);
            using var child = System.Diagnostics.Process.Start(start);
            var output = child.StandardOutput.ReadToEnd(); var error = child.StandardError.ReadToEnd();
            Check(child.WaitForExit(30000) && child.ExitCode == 0, "fresh process: " + output + error);
            Console.Write(output);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void AdapterBoundaries()
    {
        var game = Fixture();
        while (game.Resolution.HasSuspendedEffect) Accept(game, Answer(game));
        int before = game.Player2.Hero.Damage;
        var atomic = StateTaskList.Chain(new DiscoverTask(CardType.MINION), new Before());
        game.TaskQueue.Execute(atomic, game.Player1, game.Player1.Hero, null);
        Check(game.Player1.Choice != null && game.Player2.Hero.Damage == before + 2 && !game.Resolution.HasSuspendedEffect,
            "legacy atomic list must still execute work after Discover");
        bool rejected = false;
        try { StateTaskList.Chain(Recipe.Value, new Before()); }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "atomic list cannot silently drop a suspended tail");
        rejected = false;
        try { StateTaskList.Suspendable("proof/two-discovers/v1", new Before()); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "registered recipe cannot be replaced under a saved frame");

        var suspended = Fixture();
        var bytes = suspended.SaveSnapshot();
        var needle = System.Text.Encoding.UTF8.GetBytes("proof/two-discovers/v1");
        int offset = bytes.AsSpan().IndexOf(needle);
        Check(offset >= 0, "snapshot includes recipe key");
        bytes[offset] = (byte)'X';
        rejected = false;
        try { Game.LoadSnapshot(bytes); }
        catch (NotSupportedException) { rejected = true; }
        Check(rejected, "unknown effect recipe fails explicitly");
    }

    internal static void Run()
    {
        foreach (bool nested in new[] { false, true })
        for (int first = 0; first < 3; first++)
        for (int second = 0; second < 3; second++) Branches(nested, first, second);
        var lethal = Fixture();
        lethal.Player2.Hero.Damage = 28;
        Accept(lethal, Answer(lethal));
        Accept(lethal, Answer(lethal));
        Check(lethal.State == SabberStoneCore.Enums.State.COMPLETE, "resumed lethal effect finalizes game");
        AdapterBoundaries();
        FreshProcess();
        Console.WriteLine("PASS suspended effects: 18 sequential/nested branches, 36 snapshot/clone boundaries, single resume, RNG/stack/entity capture, rejection byte identity, legacy atomic adapter");
    }
}
