using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using HsSim;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;

internal static class SnapshotProof
{
    private static int _roundTrips, _futures, _rejections;
    private static object Field(object value, string name) => value.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
    private static RngState Rng(Game game) => ((Util.DeepCloneableRandom)typeof(Game)
        .GetProperty("Random", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game)).CaptureState();
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("snapshot: " + message);
    }
    private static bool Quiescent(Game game) =>
        new[] { "_baseQueue", "_pendingTasks", "_eventStack" }.All(f => ((ICollection)Field(game.TaskQueue, f)).Count == 0) &&
        game.TaskQueue.IsEmpty && game.TaskQueue.CurrentTask == null && game.DeadMinions.Count == 0 &&
        game.SummonedMinions.Count == 0 && Field(game, "ClearWeapons") == null &&
        ((ICollection)Field(game, "_rebornMinions")).Count == 0;
    private static string History(Game game) => game.PowerHistory == null ? "" : Canonical.History(game);
    private static string State(Game game) => Canonical.State(game.CloneExact());
    private static void Same(Game source, Game copy, bool history = true)
    {
        Check(State(source) == State(copy), "state hash diverged");
        Check(Rng(source).Equals(Rng(copy)), "RNG diverged");
        Check(source.NextCloneIndex == copy.NextCloneIndex && source.CloneIndex == copy.CloneIndex, "clone counters diverged");
        if (history) Check(History(source) == History(copy), "full history diverged");
        Check(source.GetPendingDecisions().Select(d => d.ToJson()).SequenceEqual(copy.GetPendingDecisions().Select(d => d.ToJson())),
            "decision ids/offers diverged");
    }
    internal static Game RoundTrip(Game game)
    {
        string state = State(game), history = History(game);
        var rng = Rng(game);
        byte[] bytes = game.SaveSnapshot();
        var copy = Game.LoadSnapshot(bytes, null);
        Check(bytes.SequenceEqual(copy.SaveSnapshot()), "save(load(save)) changed bytes");
        Check(bytes.SequenceEqual(game.SaveSnapshot()), "save mutated source state");
        Check(State(game) == state && History(game) == history && Rng(game).Equals(rng), "save changed source");
        Same(game, copy);
        EventOrdinalProof.Same(game.TriggerManager.Events, copy.TriggerManager.Events, EventOrdinalProof.Channels);
        foreach (var character in game.IdEntityDic.Values.OfType<Character>())
            EventOrdinalProof.Same(character.Events, ((Character)copy.IdEntityDic[character.Id]).Events, EventOrdinalProof.LocalChannels);
        _roundTrips++;
        return copy;
    }
    private static void Step(Game game, PlayerTask task)
    {
        Check(game.Process(task), "action rejected");
        if (game.Step == SabberStoneCore.Enums.Step.BEGIN_MULLIGAN && game.Player1.MulliganState == Mulligan.DONE &&
            game.Player2.MulliganState == Mulligan.DONE) game.MainBegin();
    }
    private static void Future(Game game, int count = 8, PolicyRandom policy = null)
    {
        var copy = RoundTrip(game);
        var clone = game.CloneExact(history: true);
        policy ??= new PolicyRandom(67);
        for (int i = 0; i < count && game.State != SabberStoneCore.Enums.State.COMPLETE; i++)
        {
            var tasks = Simulation.Legal(game);
            int index = policy.Index(tasks.Count);
            string key = Simulation.TaskKey(tasks[index]);
            foreach (Game other in new[] { copy, clone })
            {
                var legal = Simulation.Legal(other);
                Check(legal.Select(Simulation.TaskKey).SequenceEqual(tasks.Select(Simulation.TaskKey)), "future legal actions diverged");
                Step(other, legal.Single(t => Simulation.TaskKey(t) == key));
            }
            Step(game, tasks[index]);
            Same(game, copy);
            Same(game, clone, false);
            Check(Canonical.Value(game.PowerHistory.Last) == Canonical.Value(copy.PowerHistory.Last) &&
                Canonical.Value(game.PowerHistory.Last) == Canonical.Value(clone.PowerHistory.Last), "future history suffix diverged");
            _futures++;
        }
    }
    private static void Seeded(bool full)
    {
        int games = 0, checkpoints = 0, busy = 0;
        foreach (string suite in new[] { "classic", "modern" })
        {
            string[] decks = Simulation.DeckIds(suite);
            for (int deck = 0; deck < (full ? decks.Length : 1); deck++)
            foreach (int seed in full ? new[] { 1, 17 } : new[] { 17 })
            {
                var game = Simulation.Create(new GameRecord { Player = decks[deck], Opponent = decks[(deck + 1) % decks.Length], Seed = seed });
                var policy = new PolicyRandom((ulong)seed);
                int step = 0;
                while (game.State != SabberStoneCore.Enums.State.COMPLETE && (full || step < 32))
                {
                    Check(step < 4000, "seeded termination");
                    if (full || step % 8 == 0)
                    {
                        if (Quiescent(game))
                        {
                            // Continue the actual source, the restored game and CloneExact with identical actions.
                            Future(game, full ? 1 : 8, policy);
                            checkpoints++;
                            step += full ? 1 : 8;
                            continue;
                        }
                        Reject(() => game.SaveSnapshot(), "nonquiescent sampled boundary");
                        busy++;
                    }
                    var legal = Simulation.Legal(game);
                    Step(game, legal[policy.Index(legal.Count)]);
                    step++;
                }
                games++;
            }
        }
        Console.WriteLine($"PASS snapshot seeded: {games} Classic/modern games, {checkpoints} checkpoints, {busy} explicit busy rejections");
    }
    private static Game Fixture(string card)
    {
        var game = new Game(new GameConfig { RandomSeed = 91, StartPlayer = 1, SkipMulligan = true,
            Player1HeroClass = CardClass.DRUID, History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(), FormatType = FormatType.FT_WILD });
        game.StartGame();
        game.Player1.BaseMana = 10;
        Entity.FromCard(game.Player1, Cards.FromId(card), zone: game.Player1.HandZone);
        return game;
    }
    private static void Choices(bool full)
    {
        foreach (string card in full ? new[] { "UNG_941", "UNG_001", "CFM_621", "ICC_828", "TRL_096", "DAL_736" } :
            new[] { "UNG_941", "UNG_001", "CFM_621", "ICC_828", "TRL_096" })
        {
            var game = Fixture(card);
            if (card == "CFM_621") game.Player1.DeckZone.GetAll().ToList().ForEach(c => game.Player1.DeckZone.Remove(c));
            Step(game, PlayCardTask.Any(game.Player1, game.Player1.HandZone.Last()));
            if (card == "ICC_828") { game.Player1.UsedMana = 0; Step(game, HeroPowerTask.Any(game.Player1)); }
            int choices = 0;
            while (game.Player1.Choice != null)
            {
                var copy = RoundTrip(game);
                int selected = game.Player1.Choice.Choices[choices % game.Player1.Choice.Choices.Count];
                var decision = game.GetPendingDecisions().Single();
                var answer = new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id, decision.ControllerId, new[] { selected });
                Check(copy.TryAnswer(answer, out _), "restored choice rejected");
                Check(game.TryAnswer(answer, out _), "source choice rejected");
                Same(game, copy);
                Check(++choices <= 5, "choice chain terminates");
            }
            Check(choices == (card == "ICC_828" || card == "TRL_096" ? 2 : card == "CFM_621" ? 3 : card == "DAL_736" ? 5 : 1), "choice count " + card);
            // Includes the composed dynamic card after the final answer, then plays it if legal.
            var loaded = RoundTrip(game);
            if (card == "ICC_828" || card == "CFM_621")
            {
                var generated = game.Player1.HandZone.Last();
                Check(!ReferenceEquals(generated.Card, Cards.FromId(generated.Card.Id)), "generated card fixture");
                Check(!ReferenceEquals(generated.Card, loaded.IdEntityDic[generated.Id].Card), "dynamic card is game-local after load");
                foreach (Game instance in new[] { game, loaded })
                {
                    instance.Player1.UsedMana = 0;
                    var play = instance.Player1.Options().FirstOrDefault(t => t.Source?.Id == generated.Id);
                    if (play != null) Step(instance, play);
                }
                Same(game, loaded);
            }
            if (Quiescent(game)) Future(game);
        }
    }
    private static void Continuations()
    {
        var seen = new HashSet<object>(System.Collections.Generic.ReferenceEqualityComparer.Instance);
        var recipes = new List<(string card, ISimpleTask task)>();
        void Visit(object value, string card)
        {
            if (value == null || value is string || value is Delegate || value is Card || value.GetType().IsValueType || !seen.Add(value)) return;
            if (value is DiscoverTask)
            {
                var task = (ISimpleTask)Field(value, "_taskTodo");
                if (task != null) recipes.Add((card, task));
            }
            if (value is IEnumerable items) { foreach (object item in items) Visit(item, card); return; }
            if (value.GetType().Assembly != typeof(Game).Assembly) return;
            for (System.Type type = value.GetType(); type != null && type != typeof(object); type = type.BaseType)
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                    Visit(field.GetValue(value), card);
        }
        foreach (Card card in Cards.All) Visit(card.Power, card.Id);
        Check(recipes.Count > 0, "catalogue continuation inventory nonempty");
        foreach (var recipe in recipes)
        {
            var game = Fixture(recipe.card);
            var source = game.Player1.HandZone.Last();
            game.Player1.Choice = new Choice(game.Player1) { SourceId = source.Id, ChoiceType = ChoiceType.GENERAL,
                ChoiceAction = ChoiceAction.STACK, Choices = new List<int> { source.Id }, AfterChooseTask = recipe.task };
            var loaded = RoundTrip(game);
            Check(loaded.Player1.Choice.AfterChooseTask.GetType() == recipe.task.GetType(), "continuation recipe restored");
        }
        Console.WriteLine($"PASS snapshot continuation inventory: {recipes.Count} definition-backed legacy after-tasks");
    }

    private static void Registrations()
    {
        var game = Fixture("UNG_067t1");
        Entity.FromCard(game.Player1, Cards.FromId("NEW1_019"), zone: game.Player1.BoardZone);
        Entity.FromCard(game.Player2, Cards.FromId("EX1_007"), zone: game.Player2.BoardZone);
        Entity.FromCard(game.Player1, Cards.FromId("NEW1_019"), zone: game.Player1.BoardZone, zonePos: 0);
        Step(game, PlayCardTask.Any(game.Player1, game.Player1.HandZone.Last()));
        var loaded = RoundTrip(game);
        foreach (Game instance in new[] { game, loaded })
        {
            var minion = Entity.FromCard(instance.Player1, Cards.FromId("CS2_231"), zone: instance.Player1.BoardZone);
            instance.Player1.BoardZone.Remove((Minion)minion);
            instance.Player1.DeckZone.Add(minion);
            instance.DeathProcessingAndAuraUpdate();
        }
        Same(game, loaded);
        Future(game);

        game = Fixture("ICC_700");
        game.Player1.Hero.Damage = 3;
        game.Player1.Hero.TakeHeal(game.Player1.Hero, 1);
        game.AuraUpdate();
        Step(game, PlayCardTask.Any(game.Player1, game.Player1.HandZone.Last()));
        loaded = RoundTrip(game);
        foreach (Game instance in new[] { game, loaded }) Step(instance, EndTurnTask.Any(instance.CurrentPlayer));
        Same(game, loaded);

        game = Fixture("CS2_231");
        var target = game.Player1.HandZone.Last();
        SabberStoneCore.Actions.Generic.AddEnchantmentBlock(game, Cards.FromId("UNG_202e"), target, target);
        loaded = RoundTrip(game);
        foreach (Game instance in new[] { game, loaded })
            Step(instance, PlayCardTask.Any(instance.Player1, instance.IdEntityDic[target.Id]));
        Same(game, loaded);
        Future(game);

        foreach (bool history in new[] { false, true })
        {
            game = new Game(new GameConfig { RandomSeed = 7, StartPlayer = 1, SkipMulligan = true, History = history, Logging = false });
            game.StartGame();
            game.CloneIndex = "[5]";
            game.NextCloneIndex = 19;
            loaded = RoundTrip(game);
            foreach (Game instance in new[] { game, loaded }) Step(instance, EndTurnTask.Any(instance.CurrentPlayer));
            Same(game, loaded);
        }
    }

    private static string ContinueHash(Game game)
    {
        var policy = new PolicyRandom(67);
        for (int i = 0; i < 8 && game.State != SabberStoneCore.Enums.State.COMPLETE; i++)
        {
            var tasks = Simulation.Legal(game);
            Step(game, tasks[policy.Index(tasks.Count)]);
        }
        return Canonical.Sha(State(game) + History(game) + Canonical.Value(Rng(game)));
    }

    internal static void Child(string directory)
    {
        foreach (string file in Directory.GetFiles(directory, "*.snapshot").OrderBy(f => f, StringComparer.Ordinal))
        {
            byte[] data = File.ReadAllBytes(file);
            var game = Game.LoadSnapshot(data);
            Check(data.SequenceEqual(game.SaveSnapshot()), "fresh process byte stability");
            Check(ContinueHash(game) == File.ReadAllText(file + ".hash"), "fresh process future parity");
        }
        Console.WriteLine("PASS snapshot fresh-process load/continue");
    }

    private static void FreshProcess()
    {
        string directory = Path.Combine(Path.GetTempPath(), "hs-snapshot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var game = Fixture("ICC_828");
            Step(game, PlayCardTask.Any(game.Player1, game.Player1.HandZone.Last()));
            game.Player1.UsedMana = 0;
            Step(game, HeroPowerTask.Any(game.Player1));
            int index = 0;
            while (true)
            {
                string file = Path.Combine(directory, index++ + ".snapshot");
                File.WriteAllBytes(file, game.SaveSnapshot());
                File.WriteAllText(file + ".hash", ContinueHash(Game.LoadSnapshot(File.ReadAllBytes(file))));
                if (game.Player1.Choice == null) break;
                Step(game, ChooseTask.Pick(game.Player1, game.Player1.Choice.Choices[0]));
            }
            game = Fixture("TRL_096");
            Step(game, PlayCardTask.Any(game.Player1, game.Player1.HandZone.Last()));
            Step(game, ChooseTask.Pick(game.Player1, game.Player1.Choice.Choices[0]));
            string continuationFile = Path.Combine(directory, "griftah.snapshot");
            File.WriteAllBytes(continuationFile, game.SaveSnapshot());
            File.WriteAllText(continuationFile + ".hash", ContinueHash(Game.LoadSnapshot(File.ReadAllBytes(continuationFile))));
            var start = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath) { RedirectStandardOutput = true, RedirectStandardError = true };
            if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("--snapshot-child");
            start.ArgumentList.Add(directory);
            using var child = System.Diagnostics.Process.Start(start);
            string output = child.StandardOutput.ReadToEnd(), error = child.StandardError.ReadToEnd();
            Check(child.WaitForExit(30000) && child.ExitCode == 0, "fresh process: " + output + error);
            Console.Write(output);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void Reject(Action operation, string label)
    {
        bool rejected = false;
        try { operation(); }
        catch (NotSupportedException) { rejected = true; }
        catch (SerializationException) { rejected = true; }
        Check(rejected, label + " must fail explicitly");
        _rejections++;
    }
    private static void Rejections()
    {
        var game = Fixture("UNG_941");
        Step(game, PlayCardTask.Any(game.Player1, game.Player1.HandZone.Last()));
        var choice = game.Player1.Choice;
        var original = choice.AfterChooseTask;
        choice.AfterChooseTask = new CustomTask((g, c, s, t, stack) => c.BaseMana++);
        Reject(() => game.SaveSnapshot(), "opaque after-choice callback");
        choice.AfterChooseTask = original;
        var bytes = game.SaveSnapshot();
        Reject(() => Game.LoadSnapshot(bytes[..^1]), "truncated input");
        Reject(() => Game.LoadSnapshot(bytes.Concat(new byte[] { 0 }).ToArray()), "trailing input");
        var badVersion = (byte[])bytes.Clone();
        using (var stream = new MemoryStream(badVersion))
        using (var reader = new BinaryReader(stream))
        { reader.ReadString(); badVersion[stream.Position] = 99; }
        Reject(() => Game.LoadSnapshot(badVersion), "unsupported version");
        Reject(() => Game.LoadSnapshot(bytes, new GameConfig { FormatType = FormatType.FT_STANDARD }), "config mismatch");
        foreach (string queue in new[] { "base", "pending", "event", "current" })
        {
            var busy = Game.LoadSnapshot(bytes);
            ISimpleTask task = new CustomTask((g, c, s, t, stack) => { });
            if (queue == "base") busy.TaskQueue.EnqueueBase(task, busy.Player1, busy.Player1.Hero, null);
            if (queue == "pending") busy.TaskQueue.EnqueuePendingTask(task, busy.Player1, busy.Player1.Hero, null);
            if (queue == "event") busy.TaskQueue.StartEvent();
            if (queue == "current") busy.TaskQueue.Enqueue(task, busy.Player1, busy.Player1.Hero, null);
            Reject(() => busy.SaveSnapshot(), queue + " queue");
        }
        foreach (string pending in new[] { "dead", "summoned", "reborn", "weapon" })
        {
            // Suspended spell completion now permits deferred cleanup; test unsupported work while idle.
            var busy = Fixture("CS2_231");
            var minion = (Minion)Entity.FromCard(busy.Player1, Cards.FromId("CS2_231"), zone: busy.Player1.BoardZone);
            if (pending == "dead") busy.DeadMinions.Add(minion);
            if (pending == "summoned") busy.SummonedMinions.Add(minion);
            if (pending == "reborn") ((IList)Field(busy, "_rebornMinions")).Add(minion);
            if (pending == "weapon") typeof(Game).GetField("ClearWeapons", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(busy, new List<int> { busy.Player1.Hero.Id });
            Reject(() => busy.SaveSnapshot(), pending + " work");
        }
        var direct = Fixture("CS2_231");
        direct.TaskQueue.Execute(new CustomTask((g, c, s, t, stack) =>
            Reject(() => g.SaveSnapshot(), "inside direct task execution")), direct.Player1, direct.Player1.Hero, null);
        int during = 0;
        game.TriggerManager.EndTurnTrigger += _ => { Reject(() => game.SaveSnapshot(), "inside Process"); during++; };
        Step(game, ChooseTask.Pick(game.Player1, game.Player1.Choice.Choices[0]));
        Step(game, EndTurnTask.Any(game.Player1));
        Check(during == 1, "reentrant rejection exercised");
    }
    internal static void SelectorCloneIsolation()
    {
        var game = PlayerDecisionProof.Fixture();
        var held = SabberStoneCore.Actions.Generic.DrawCard(game.Player1, Cards.FromId("CORE_BT_801"));
        Core2026Proof.Play(game, game.Player1, "CATA_697");
        var bytes = game.SaveSnapshot(); var clone = game.CloneExact(history: true); var restored = Game.LoadSnapshot(bytes);
        Check(bytes.SequenceEqual(restored.SaveSnapshot()), "selector byte-stable restore");
        Check(!ReferenceEquals(game.Resolution.SuspendedEffect.OfferEntityIds, clone.Resolution.SuspendedEffect.OfferEntityIds) &&
            (game.Resolution.SuspendedEffect.SelectedInGroup.Length == 0 || !ReferenceEquals(game.Resolution.SuspendedEffect.SelectedInGroup, clone.Resolution.SuspendedEffect.SelectedInGroup)), "selector arrays cloned");
        PlayerDecisionProof.Answer(clone, held.Id);
        Check(bytes.SequenceEqual(game.SaveSnapshot()), "selector clone answering cannot alter original");
        PlayerDecisionProof.Answer(restored, held.Id); PlayerDecisionProof.Answer(game, held.Id);
        Same(game, restored); Same(game, clone, history: false);
    }

    internal static void CorruptSelectorGroupHistoryRejected(Game game)
    {
        var bytes = game.SaveSnapshot();
        foreach (var corrupt in new Action<SabberStoneCore.Model.Effects.EffectFrame>[] {
            f => f.SelectedInGroup = f.SelectedInGroup.Skip(1).ToArray(),
            f => f.SelectedInGroup = f.SelectedInGroup.Concat(new[] { f.SelectedInGroup[0] }).ToArray(),
            f => f.SelectorHistory = f.SelectorHistory.Skip(1).ToArray(),
            f => f.SelectorHistory[0].ChosenIndex = f.ChosenEntities.Length,
            f => f.SelectorHistory[0].ControllerId = game.Player2.Id,
            f => f.SelectorHistory[0].Zone = Zone.DECK
        })
        {
            var copy = Game.LoadSnapshot(bytes);
            corrupt(copy.Resolution.SuspendedEffect);
            var wire = new SabberStoneCore.Model.Snapshots.SnapshotCodec().Save(copy);
            Reject(() => Game.LoadSnapshot(wire), "deleted/duplicated recorded selector pick");
        }
    }

    internal static void CorruptSelectorDescriptorRejected()
    {
        var game = PlayerDecisionProof.Fixture();
        SabberStoneCore.Actions.Generic.DrawCard(game.Player1, Cards.FromId("CORE_BT_801"));
        Core2026Proof.Play(game, game.Player1, "CATA_697");
        var bytes = game.SaveSnapshot();
        foreach (var corrupt in new Action<SabberStoneCore.Model.Effects.EffectFrame>[] {
            f => f.OfferEntityIds = new[] { f.OfferEntityIds[0], f.OfferEntityIds[0] },
            f => f.OfferEntityIds = new[] { game.Player2.Hero.Id },
            f => f.SelectionGroupKey = "unknown-group",
            f => f.SelectedInGroup = new[] { f.OfferEntityIds[0] },
            f => f.NextStep = 99,
            f => typeof(SabberStoneCore.Model.Effects.EffectFrame).GetProperty("RecipeKey").SetValue(f, "client/BE_036/v1"),
            f => typeof(SabberStoneCore.Model.Effects.EffectFrame).GetProperty("ControllerId").SetValue(f, game.Player2.Id)
        })
        {
            var copy = Game.LoadSnapshot(bytes); corrupt(copy.Resolution.SuspendedEffect);
            var wire = new SabberStoneCore.Model.Snapshots.SnapshotCodec().Save(copy);
            Reject(() => Game.LoadSnapshot(wire), "corrupt selector frame descriptor");
        }
        foreach (var invalid in new Action[] {
            () => new SabberStoneCore.Model.Effects.SelectorSpec((SabberStoneCore.Model.Effects.SelectorSource)99),
            () => new SabberStoneCore.Model.Effects.SelectorSpec(SabberStoneCore.Model.Effects.SelectorSource.Hand, SabberStoneCore.Model.Effects.SelectorSide.Enemy),
            () => new SabberStoneCore.Model.Effects.SelectorSpec(SabberStoneCore.Model.Effects.SelectorSource.Hand, excludeEarlierPicks: true),
            () => new SabberStoneCore.Model.Effects.SelectorSpec(SabberStoneCore.Model.Effects.SelectorSource.Hand, maximumCurrentCost: -1)
        })
        {
            bool rejected = false;
            try { invalid(); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "invalid immutable selector rejected at definition boundary");
        }
    }

    internal static void Run(bool full)
    {
        _roundTrips = _futures = _rejections = 0;
        Seeded(full);
        Choices(full);
        Continuations();
        Registrations();
        Rejections();
        FreshProcess();
        Console.WriteLine($"PASS snapshots: {_roundTrips} stable round trips, {_futures} identical source/load/CloneExact future actions, {_rejections} rejection proofs; Discover/Adapt/Kazakus/Build-A-Beast");
    }
}
