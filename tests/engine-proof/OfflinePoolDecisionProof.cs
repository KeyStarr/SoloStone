using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;

internal static class OfflinePoolDecisionProof
{
    private static void Check(bool value, string label)
    { if (!value) throw new Exception("offline pool decision: " + label); }
    private static EffectContext Context(Game game, IPlayable source = null) =>
        new(game, game.CurrentPlayer.Id, (source ?? game.CurrentPlayer.Hero).Id);
    private static string[] Offers(Game game) => game.CurrentPlayer.Choice.Choices.Select(id => game.IdEntityDic[id].Card.Id).ToArray();
    private static DecisionAnswer Answer(Game game, int index = 0)
    {
        var view = game.GetPendingDecisions().Single();
        return new(view.Id, view.ControllerId, new[] { view.Options[index].OptionId });
    }
    private static void Accept(Game game, DecisionAnswer answer) => Check(game.TryAnswer(answer, out var error), error);
    private static void Reject(Game game, DecisionAnswer answer)
    {
        byte[] before = game.SaveSnapshot();
        Check(!game.TryAnswer(answer, out _), "invalid/stale answer accepted");
        Check(before.SequenceEqual(game.SaveSnapshot()), "rejection changed state/history/RNG");
    }
    private static void InvalidAnswers(Game game)
    {
        var view = game.GetPendingDecisions().Single();
        int id = view.Options[0].OptionId;
        Reject(game, new(view.Id, game.CurrentPlayer.Opponent.Id, new[] { id }));
        Reject(game, new(view.Id, view.ControllerId, Array.Empty<int>()));
        Reject(game, new(view.Id, view.ControllerId, new[] { int.MaxValue }));
        Reject(game, new(view.Id, view.ControllerId, new[] { id, id }));
    }
    private static void Same(Game a, Game b)
    {
        Check(Canonical.State(a) == Canonical.State(b), "clone/load state");
        Check(a.Random.CaptureState().Equals(b.Random.CaptureState()), "clone/load RNG");
        Check(a.OfflinePools.Hash == b.OfflinePools.Hash, "clone/load manifest");
        Check(a.GetPendingDecisions().Select(d => d.ToJson()).SequenceEqual(b.GetPendingDecisions().Select(d => d.ToJson())), "clone/load offers");
    }
    private static string Future(Game game) => string.Join(",", Enumerable.Range(0, 12)
        .Select(_ => Context(game).PickRandomCard(OfflinePoolFilter.Random()).Id));

    private static void Ordinary()
    {
        Check(OfflinePoolFilter.Discover().ClassMode == PoolClassMode.ControllerAndNeutral &&
            OfflinePoolFilter.Random().ClassMode == PoolClassMode.Any, "named factory defaults");
        foreach (int count in new[] { 0, 1, 2, 3 })
        {
            var game = PinnedPoolFixture.Use(OfflinePoolTestRecipes.NewGame());
            var source = Entity.FromCard(game.Player1, Cards.FromId(count == 2 ? "EX1_028" : "CS2_231"));
            var filter = count == 0 ? OfflinePoolTestRecipes.Empty : count == 1 ?
                OfflinePoolFilter.Random(scope: PoolScope.Past, race: Race.PIRATE) : OfflinePoolTestRecipes.Past;
            var before = game.SaveSnapshot(); var rng = game.Random.CaptureState(); int entities = game.IdEntityDic.Count;
            bool opened = Context(game, source).Discover(filter, afterChoose: new ArmorTask(1));
            Check(opened == (count > 0), "0-3 candidate return value");
            Check(game.Random.CaptureState().Equals(rng), "small pool consumes no RNG");
            if (count == 0)
            {
                Check(game.Player1.Choice == null && before.SequenceEqual(game.SaveSnapshot()), "empty Discover creates no choice/entity");
                Check(Context(game, source).PickRandomCard(filter) == null && before.SequenceEqual(game.SaveSnapshot()), "empty random creates nothing/uses no RNG");
                continue;
            }
            var expected = OfflineCardPools.Candidates(game, CardClass.MAGE, filter, source.Card.AssetId);
            Check(Offers(game).SequenceEqual(expected.Select(c => c.Id)) && Offers(game).Length == count &&
                game.IdEntityDic.Count == entities + count, "small offers sorted, source excluded, exact entity count");
            InvalidAnswers(game);
            var clone = game.CloneExact(history: true); var loaded = PinnedPoolFixture.LoadSnapshot(game.SaveSnapshot());
            Same(game, clone); Same(game, loaded);
            var answer = Answer(game); int chosen = answer.SelectedOptionIds.Single();
            foreach (var g in new[] { game, clone, loaded })
            {
                Accept(g, answer);
                Check(g.Player1.Choice == null && g.Player1.HandZone.Any(c => c.Id == chosen) && g.Player1.Hero.Armor == 1,
                    "ordinary selected entity moves to hand; afterChoose runs once");
                Reject(g, answer);
            }
            Same(game, clone); Same(game, loaded);
            string future = Future(game);
            Check(future == Future(clone) && future == Future(loaded), "ordinary snapshot/clone future picks");
        }
        {
            var game = OfflinePoolTestRecipes.NewGame();
            var source = Entity.FromCard(game.Player1, Cards.FromId("GVG_070"));
            var filter = OfflinePoolFilter.Random(scope: PoolScope.Past, race: Race.PIRATE);
            byte[] before = game.SaveSnapshot();
            Check(Context(game, source).PickRandomCard(filter) == null && !Context(game, source).Discover(filter) &&
                before.SequenceEqual(game.SaveSnapshot()), "source identity excludes the only candidate without RNG/entities");
            Check(new DiscoverThenContinueTask(OfflinePoolTestRecipes.Empty).Open(game, game.Player1, source, null, new TaskStack()) == TaskState.COMPLETE &&
                before.SequenceEqual(game.SaveSnapshot()), "empty suspended Open completes with no state/entity/RNG change");
            var stack = new TaskStack { Playables = new List<IPlayable> { source } };
            new DiscoverThenContinueTask(OfflinePoolTestRecipes.Empty).Open(game, game.Player1, source, null, stack);
            Check(stack.Playables.Count == 0, "empty suspended Discover clears stale answers");
        }
        foreach (bool opponent in new[] { false, true })
        {
            var game = OfflinePoolTestRecipes.NewGame(opponent);
            var filter = OfflinePoolFilter.Discover();
            var sourceCard = Cards.FromId("CS2_231");
            var source = Entity.FromCard(game.CurrentPlayer, sourceCard);
            var reference = game.CloneExact();
            var expected = OfflineCardPools.SampleDistinct(reference, filter, game.CurrentPlayer.HeroClass, source.Card.AssetId);
            Check(Context(game, source).Discover(filter), "default Discover opens");
            Check(Offers(game).SequenceEqual(expected.Select(c => c.Id)) && !Offers(game).Contains(source.Card.Id), "hero class/source passed to sampler");
            Check(game.Random.CaptureState().Equals(reference.Random.CaptureState()), "large Discover exactly three draws");
            var before = game.SaveSnapshot(); Bot(game); Check(before.SequenceEqual(game.SaveSnapshot()), "ordinary bot isolation");
            var expectedPick = OfflineCardPools.Pick(reference, OfflinePoolFilter.Random(), game.CurrentPlayer.HeroClass, source.Card.AssetId);
            Check(Context(game, source).PickRandomCard(OfflinePoolFilter.Random()).Id == expectedPick.Id &&
                game.Random.CaptureState().Equals(reference.Random.CaptureState()), "random wrapper source/class/RNG");
        }
    }

    private static void Queued()
    {
        var game = OfflinePoolTestRecipes.NewGame(); var context = Context(game);
        Check(context.Discover(OfflinePoolTestRecipes.Past, afterChoose: new ArmorTask(1)), "first queue");
        Check(!context.Discover(OfflinePoolTestRecipes.Empty), "empty doesn't append queue");
        Check(context.Discover(OfflinePoolTestRecipes.Past, afterChoose: new ArmorTask(2)), "second queue");
        Check(game.Player1.Choice.NextChoice != null && game.Player1.Choice.NextChoice.NextChoice == null, "two queued choices");
        var loaded = Game.LoadSnapshot(game.SaveSnapshot()); var clone = game.CloneExact();
        for (int stage = 0; stage < 2; stage++)
        {
            InvalidAnswers(game);
            var answer = Answer(game, stage); var rng = game.Random.CaptureState();
            foreach (var g in new[] { game, clone, loaded }) { Accept(g, answer); Reject(g, answer); }
            Check(game.Random.CaptureState().Equals(rng), "queue answer consumes no RNG");
            Check(game.Player1.Hero.Armor == (stage == 0 ? 1 : 3), "queued afterChoose exactly once");
            Same(game, clone); Same(game, loaded);
        }
        Check(game.Player1.Choice == null, "queue drained");
    }

    private static Game Play(Card card)
    {
        var game = OfflinePoolTestRecipes.NewGame();
        var source = Generic.DrawCard(game.Player1, card);
        Check(game.Process(PlayCardTask.Any(game.Player1, source)), "test source play");
        return game;
    }
    private static void Suspended()
    {
        OfflinePoolTestRecipes.WithSource(OfflinePoolTestRecipes.NoOffers, card => {
            var game = Play(card);
            Check(game.Player1.HandZone.All(c => c.Card.Id != "EX1_319"), "no answer adds no stale source to hand");
            Check(game.Player1.Choice == null && !game.Resolution.HasSuspendedEffect && game.Player1.Hero.Armor == 7,
                "empty suspended Discover continues remaining effects");
            Check(!game.Player1.SetasideZone.Any(), "empty suspended Discover creates no offers");
        });
        OfflinePoolTestRecipes.WithSource(OfflinePoolTestRecipes.Dynamic, card => {
            var game = Play(card); int hand = game.Player1.HandZone.Count;
            for (int stage = 0; stage < 2; stage++)
            {
                Check(game.GetPendingDecisions().Single().Kind == DecisionKind.SuspendedEffect, "suspended decision kind");
                Check(game.Player1.Choice.ChoiceAction == ChoiceAction.STACK, "suspended action STACK");
                Check(game.Player1.Choice.Choices.All(id => game.IdEntityDic[id].Card.Cost == (stage == 0 ? 5 : 1)), "dynamic binding resolved at each boundary");
                Check(game.Player1.Choice.Choices.All(id => game.IdEntityDic[id][Generic.PrivateDecisionOfferTag] == 1), "private decision offers tagged");
                InvalidAnswers(game); Bot(game);
                byte[] bytes = game.SaveSnapshot(); var loaded = Game.LoadSnapshot(bytes); var clone = game.CloneExact(history: true);
                Check(bytes.SequenceEqual(loaded.SaveSnapshot()), "suspended byte round trip");
                Same(game, clone); Same(game, loaded);
                var answer = Answer(game, stage);
                foreach (var g in new[] { game, clone, loaded })
                {
                    Accept(g, answer); Reject(g, answer);
                    Check(g.Player1.Hero.Armor == stage + 1 && g.Player1.HandZone.Count == hand + stage + 1,
                        "suspended selection resumes once");
                }
                Same(game, clone); Same(game, loaded);
                Check(Canonical.History(game) == Canonical.History(loaded), "loaded continuation history");
                string future = Future(game.CloneExact());
                Check(future == Future(clone) && future == Future(loaded), "suspended future draws");
            }
            Check(game.Player1.Choice == null && !game.Resolution.HasSuspendedEffect, "suspension finished");
        });
    }
    private static void Bot(Game game)
    {
        byte[] before = game.SaveSnapshot();
        var field = typeof(FairBotState).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic);
        var search = FairBotState.Create(game, game.CurrentPlayer.PlayerId, 901, DeckStyle.Value);
        var searchGame = (Game)field.GetValue(search);
        Check(ReferenceEquals(game.OfflinePools, searchGame.OfflinePools) && !ReferenceEquals(game.Random, searchGame.Random), "search manifest identity and independent RNG");
        Check(searchGame.Random.CaptureState().Equals(new Util.DeepCloneableRandom(901).CaptureState()), "search seed");
        Check(search.CanExpand && search.Actions.Length == game.CurrentPlayer.Choice.Choices.Count, "search enumerates every offer");
        var searchRng = searchGame.Random.CaptureState();
        foreach (var action in search.Actions)
        {
            var child = search.Apply(action); var copy = (Game)field.GetValue(child);
            Check(ReferenceEquals(game.OfflinePools, copy.OfflinePools), "Apply preserves manifest");
            var repeated = (Game)field.GetValue(search.Apply(action));
            Check(Canonical.State(copy) == Canonical.State(repeated) && copy.Random.CaptureState().Equals(repeated.Random.CaptureState()),
                "repeat expansion state/RNG deterministic");
            Check(searchRng.Equals(searchGame.Random.CaptureState()) && !ReferenceEquals(searchGame.Random, copy.Random), "Apply isolates branch RNG");
        }
        Check(before.SequenceEqual(game.SaveSnapshot()), "Create/Apply leaves authoritative state/history/RNG unchanged");
    }
    private static void Legacy()
    {
        var game = OfflinePoolTestRecipes.NewGame(legacy: true); var context = Context(game);
        foreach (var filter in new[] { OfflinePoolTestRecipes.Empty, OfflinePoolTestRecipes.Past })
        foreach (Action invoke in new Action[] { () => context.Discover(filter), () => context.PickRandomCard(filter),
            () => new DiscoverThenContinueTask(filter).Open(game, game.Player1, game.Player1.Hero, null, new TaskStack()),
            () => new DiscoverThenContinueTask(filter, PoolCostBinding.StackNumber).Open(game, game.Player1, game.Player1.Hero, null, new TaskStack { Number = 5 }) })
        {
            byte[] bytes = game.SaveSnapshot(); bool rejected = false;
            try { invoke(); } catch (InvalidOperationException e) { rejected = e.Message.Contains("legacy replay"); }
            Check(rejected && bytes.SequenceEqual(game.SaveSnapshot()), "legacy entry point rejects before side effects");
        }
    }
    private static void Undeclared()
    {
        var game = OfflinePoolTestRecipes.NewGame();
        foreach (var source in new IPlayable[] { game.Player1.Hero,
            Entity.FromCard(game.Player1, Cards.FromId("CATA_484")) })
        foreach (Action invoke in new Action[] {
            () => Context(game, source).Discover(OfflinePoolTestRecipes.Empty),
            () => Context(game, source).PickRandomCard(OfflinePoolTestRecipes.Empty),
            () => Context(game, source).Discover(OfflinePoolTestRecipes.Empty, recipeDbfId: 120782),
            () => Context(game, source).PickRandomCard(SabberStoneCore.CardSets.Client.OfflinePoolDeclarations.PastFiveCostMinion(), recipeDbfId: 123398),
            () => Context(game, source).PickRandomCard(SabberStoneCore.CardSets.Client.OfflinePoolDeclarations.PastFiveCostMinion(), PoolCostBinding.StackNumber, recipeDbfId: 120782),
            () => Context(game, source).PickRandomCard(SabberStoneCore.CardSets.Client.OfflinePoolDeclarations.PastFiveCostMinion(), recipeDbfId: -1),
            () => new DiscoverThenContinueTask(OfflinePoolTestRecipes.Empty).Open(game, game.Player1, source, null, new TaskStack()),
            () => new DiscoverThenContinueTask(SabberStoneCore.CardSets.Client.OfflinePoolDeclarations.AnyClassOneCostSpell(), PoolCostBinding.StackNumber)
                .Open(game, game.Player1, source, null, new TaskStack { Number = 1 }) })
        {
            byte[] bytes = game.SaveSnapshot(); bool rejected = false;
            try { invoke(); } catch (InvalidOperationException e) { rejected = e.Message.Contains("declared pool"); }
            Check(rejected && bytes.SequenceEqual(game.SaveSnapshot()), "undeclared recipe/filter/binding rejects before side effects");
        }
    }
    private static void MaximumCostBinding()
    {
        var binding = (PoolCostBinding)2;
        Check(Enum.GetName(typeof(PoolCostBinding), binding) == "StackNumberMaxCost", "appended maximum binding identity");
        var game = OfflinePoolTestRecipes.NewGame();
        typeof(Game).GetProperty(nameof(Game.OfflinePools)).SetValue(game,
            OfflinePoolFixtures.Manifest(OfflinePoolFixtures.Fixture()));
        var slots = (IList<OfflinePoolDeclaration>)typeof(System.Collections.ObjectModel.ReadOnlyCollection<OfflinePoolDeclaration>)
            .GetProperty("Items", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(SabberStoneCore.CardSets.Client.OfflinePoolDeclarations.Cards);
        var original = slots[0];
        var source = Entity.FromCard(game.Player1, Cards.FromId("CS2_231"));
        var template = OfflinePoolFilter.Random(minCost: 1, maxCost: 99);
        var declaration = new OfflinePoolDeclaration(source.Card.Id, source.Card.AssetId,
            new PoolRequirement("maximum", template, new[] { "MAGE" }, binding, 3),
            new PoolRequirement("exact", template, new[] { "MAGE" }, PoolCostBinding.StackNumber, 3));
        try
        {
            slots[0] = declaration;
            var counts = OfflinePoolAdmission.Evaluate(game.OfflinePools, new[] { declaration },
                new Dictionary<int, string>()).Cards.Single().PoolRequirements;
            Check(counts.Single(r => r.Name == "maximum" && r.Binding == "ordinary").Count == 3
                && counts.Single(r => r.Name == "exact" && r.Binding == "ordinary").Count == 1,
                "admission max bound differs from exact cost");
            var stack = new TaskStack { Number = 3 };
            var context = new EffectContext(game, game.Player1.Id, source.Id, stack: stack);
            var reference = game.CloneExact();
            var expected = OfflineCardPools.Pick(reference, OfflinePoolFilter.Random(minCost: 1, maxCost: 3),
                CardClass.MAGE, source.Card.AssetId);
            Check(context.PickRandomCard(template, binding).Id == expected.Id
                && game.Random.CaptureState().Equals(reference.Random.CaptureState()), "max binding random candidates/RNG");
            Check(context.Discover(template, costBinding: binding), "max binding Discover opens");
            Check(Offers(game).Length == 3, "max binding three fixture candidates");
            // Fixture catalog costs intentionally differ from definitions; compare catalog identities.
            Check(Offers(game).ToHashSet().SetEquals(OfflineCardPools.Candidates(game.OfflinePools,
                CardClass.MAGE, OfflinePoolFilter.Random(minCost: 1, maxCost: 3), source.Card.AssetId).Select(c => c.Id)),
                "max binding includes cheaper cards and equality, excludes above bound");
            Accept(game, Answer(game));
            Check(context.Discover(template, costBinding: PoolCostBinding.StackNumber)
                && game.Player1.Choice.Choices.Count == 1, "exact StackNumber behavior unchanged");
        }
        finally { slots[0] = original; }
    }

    internal static void Run()
    {
        MaximumCostBinding();
        Undeclared();
        OfflinePoolTestRecipes.WithDeclarations(() => { Ordinary(); Queued(); Suspended(); Legacy(); });
        Console.WriteLine("PASS offline pool decisions: ordinary/suspended; 0-3 offers; queues; invalid/stale byte identity; single resume; dynamic cost; clone/snapshot future draws; FairBotState manifest/RNG; legacy rejection");
    }
}
