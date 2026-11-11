using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using HsSim;
using SabberStoneCore.Config;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks;

internal static class DecisionProof
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("decision: " + message);
    }

    private static object Field(object value, string name) => value.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
    private static RngState Rng(Game game) => ((Util.DeepCloneableRandom)typeof(Game)
        .GetProperty("Random", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game)).CaptureState();

    private static string ChoiceState(Choice choice) => choice == null ? "null" :
        $"{choice.DecisionId}:{choice.Controller.Id}:{choice.SourceId}:{choice.ChoiceType}:{choice.ChoiceAction}:" +
        $"{choice.LastChoice}:{string.Join(",", choice.Choices ?? new())}:{Canonical.Value(Field(choice, "<EntityStack>k__BackingField"))}:" +
        $"{choice.AfterChooseTask?.GetType().FullName}:" + ChoiceState(choice.NextChoice);

    // Canonical.State reads lazy costs; read it on a clone so the hash itself cannot warm source caches.
    private static string Fingerprint(Game game)
    {
        var costs = typeof(Playable).GetField("_modifiedCost", BindingFlags.Instance | BindingFlags.NonPublic);
        return Canonical.Sha(Canonical.State(game.CloneExact()) + Canonical.History(game) + Canonical.Value(game.PowerHistory.Last) +
            Canonical.Value(Rng(game)) + ChoiceState(game.Player1.Choice) + ChoiceState(game.Player2.Choice) +
            Canonical.Value(game.IdEntityDic.Select(p => p).OrderBy(p => p.Key).Select(p => new {
                p.Key, Tags = p.Value.NativeTags, CostCache = p.Value is Playable ? costs.GetValue(p.Value) : null
            })) + $"{Field(game, "_idIndex")}:{Field(game, "_oopIndex")}:{Field(game, "_nextDecisionId")}:" +
            $"{Field(game, "_optionsDecisionId")}:{game.NextCloneIndex}:{game.CloneIndex}:" +
            $"{game.EntityChoicesMap.Count}:{game.AllOptionsMap.Count}:" +
            Canonical.Value(game.SummonedMinions.Select(m => m.Id)) + Canonical.Value(game.DeadMinions.Select(m => m.Id)));
    }

    private static void Reject(Game game, DecisionAnswer answer, string label)
    {
        string before = Fingerprint(game);
        Check(!game.TryAnswer(answer, out string error) && !string.IsNullOrEmpty(error), label + " rejected");
        Check(Fingerprint(game) == before, label + " changed state/history/RNG/counters/cost caches");
    }

    private static IReadOnlyList<PendingDecision> Views(Game game)
    {
        var views = game.GetPendingDecisions();
        foreach (var view in views)
        {
            Check(view.SchemaVersion == 1, "schema version");
            string json = view.ToJson();
            Check(PendingDecision.FromJson(json).ToJson() == json, "deterministic view JSON round-trip");
            Check(((IList<DecisionOption>)view.Options).IsReadOnly, "offers immutable");
        }
        return views;
    }

    private static DecisionAnswer Answer(PendingDecision view, PlayerTask legacy)
    {
        if (legacy is ChooseTask choice) return new DecisionAnswer(view.Id, view.ControllerId, choice.Choices);
        int entity = legacy.Source?.Id ?? (legacy.PlayerTaskType == PlayerTaskType.HERO_ATTACK ? legacy.Controller.Hero.Id :
            legacy.PlayerTaskType == PlayerTaskType.HERO_POWER ? legacy.Controller.Hero.HeroPower.Id : 0);
        var option = view.Options.Single(o => o.TaskType == legacy.PlayerTaskType && o.EntityId == entity &&
            o.TargetId == (legacy.Target?.Id ?? 0) && o.BranchIndex == legacy.ChooseOne &&
            o.ZonePosition == (legacy is PlayCardTask play ? play.ZonePosition : -1));
        return new DecisionAnswer(view.Id, view.ControllerId, new[] { option.OptionId });
    }

    private static void Accept(Game game, DecisionAnswer answer)
    {
        string json = answer.ToJson();
        var restored = DecisionAnswer.FromJson(json);
        Check(restored.ToJson() == json, "answer JSON round-trip");
        Check(game.TryAnswer(restored, out string error) && error == null, "answer accepted: " + error);
    }

    private static void BeginAfterMulligans(Game game)
    {
        // This is the existing host contract; neither legacy Process nor the adapter changes it.
        if (game.Step == Step.BEGIN_MULLIGAN && game.Player1.MulliganState == Mulligan.DONE &&
            game.Player2.MulliganState == Mulligan.DONE) game.MainBegin();
    }

    private static void Same(Game game, Game other, bool fullHistory = true)
    {
        Check(Canonical.State(game) == Canonical.State(other), "legacy/clone state parity");
        Check(Rng(game).Equals(Rng(other)), "legacy/clone RNG parity");
        Check(Canonical.Value(game.PowerHistory.Last) == Canonical.Value(other.PowerHistory.Last), "history suffix byte parity");
        if (fullHistory) Check(Canonical.History(game) == Canonical.History(other), "full history byte parity");
        Check(Views(game).Select(d => d.ToJson()).SequenceEqual(Views(other).Select(d => d.ToJson())), "future offers and IDs parity");
    }

    private static Game MulliganGame() => Simulation.Create(new GameRecord {
        Player = Simulation.DeckIds("classic")[0], Opponent = Simulation.DeckIds("classic")[1], Seed = 21
    });

    private static void Mulligans()
    {
        foreach (bool reverse in new[] { false, true })
        foreach (bool keepAll in new[] { false, true })
        {
            var game = MulliganGame();
            var legacy = MulliganGame();
            string before = Fingerprint(game);
            var open = Views(game);
            Check(open.Count == 2 && open.Select(v => v.Id).Distinct().Count() == 2, "both mulligans concurrent, distinct IDs");
            Check(Fingerprint(game) == before, "view reads cannot mutate source");
            foreach (var view in reverse ? open.Reverse() : open)
            {
                Check(view.Kind == DecisionKind.Mulligan && view.SelectionsAreKeepList && view.MinSelections == 0 &&
                    view.MaxSelections == view.Options.Count, "mulligan keep semantics/cardinality");
                int first = view.Options[0].OptionId;
                Reject(game, null, "null");
                Reject(game, new DecisionAnswer(view.Id, 999, new[] { first }), "unknown owner");
                Reject(game, new DecisionAnswer(view.Id, view.ControllerId == game.Player1.Id ? game.Player2.Id : game.Player1.Id,
                    new[] { first }), "cross-controller");
                Reject(game, new DecisionAnswer(long.MaxValue, view.ControllerId, new[] { first }), "unknown ID");
                Reject(game, new DecisionAnswer(view.Id, view.ControllerId, new[] { first, first }), "duplicate keep");
                Reject(game, new DecisionAnswer(view.Id, view.ControllerId, new[] { int.MaxValue }), "unoffered keep");
                Reject(game, new DecisionAnswer(view.Id, view.ControllerId, Enumerable.Range(0, view.MaxSelections + 1)), "too many keeps");
                int[] kept = keepAll ? view.Options.Select(o => o.EntityId).ToArray() : Array.Empty<int>();
                var answer = new DecisionAnswer(view.Id, view.ControllerId, kept);
                var clone = game.CloneExact(history: true);
                Accept(clone, DecisionAnswer.FromJson(answer.ToJson()));
                Accept(game, answer);
                Check(legacy.Process(ChooseTask.Mulligan(legacy.ControllerById(view.ControllerId), kept.ToList())), "legacy mulligan accepted");
                Same(game, legacy);
                Same(game, clone, fullHistory: false);
                Check(kept.All(id => game.ControllerById(view.ControllerId).HandZone.Any(c => c.Id == id)), "selected cards kept");
                Reject(game, answer, "duplicate submission");
            }
            Check(game.GetPendingDecisions().Count == 0, "host MainBegin boundary unchanged");
            BeginAfterMulligans(game);
            BeginAfterMulligans(legacy);
            Same(game, legacy);
        }
    }

    private static bool IsQuiescent(Game game) =>
        new[] { "_baseQueue", "_pendingTasks", "_eventStack" }.All(field => ((ICollection)Field(game.TaskQueue, field)).Count == 0) &&
        game.TaskQueue.IsEmpty && game.TaskQueue.CurrentTask == null && game.DeadMinions.Count == 0 &&
        game.SummonedMinions.Count == 0 && Field(game, "ClearWeapons") == null &&
        ((ICollection)Field(game, "_rebornMinions")).Count == 0;

    private static void AssertQuiescent(Game game) => Check(IsQuiescent(game), "checkpoint is quiescent");

    private static void SeededGames(bool full)
    {
        int games = 0, boundaries = 0, checkpoints = 0;
        foreach (string suite in new[] { "classic", "modern" })
        {
            var decks = Simulation.DeckIds(suite);
            for (int deck = 0; deck < (full ? decks.Length : 1); deck++)
            foreach (int seed in full ? new[] { 1, 17 } : new[] { 17 })
            {
                var record = new GameRecord { Player = decks[deck], Opponent = decks[(deck + 1) % decks.Length], Seed = seed };
                var game = Simulation.Create(record);
                var legacy = Simulation.Create(record);
                var policy = new PolicyRandom((ulong)seed);
                int step = 0;
                while (game.State != State.COMPLETE && (full || step < 24))
                {
                    Check(step++ < 4000, "seeded game terminates");
                    var open = Views(game);
                    var legal = Simulation.Legal(legacy);
                    var task = legal[policy.Index(legal.Count)];
                    var view = open.Single(v => v.ControllerId == task.Controller.Id);
                    var restoredView = PendingDecision.FromJson(view.ToJson());
                    var answer = Answer(restoredView, task);
                    var clone = IsQuiescent(game) ? game.CloneExact(history: true) : null;
                    Check(clone == null || clone.GetPendingDecisions().Select(v => v.ToJson()).SequenceEqual(open.Select(v => v.ToJson())), "clone preserves serialized decisions");
                    Reject(game, new DecisionAnswer(view.Id, view.ControllerId, new[] { int.MaxValue }), "invalid seeded answer");
                    if (clone != null) Accept(clone, answer);
                    Accept(game, answer);
                    Check(legacy.Process(task), "legacy seeded task accepted");
                    BeginAfterMulligans(game);
                    BeginAfterMulligans(legacy);
                    if (clone != null) BeginAfterMulligans(clone);
                    Same(game, legacy);
                    if (clone != null) { Same(game, clone, fullHistory: false); checkpoints++; }
                    Reject(game, answer, "stale seeded answer");
                    boundaries++;
                }
                if (full) Check(Views(game).Count == 0, "terminal game has no offers");
                games++;
            }
        }
        Console.WriteLine($"PASS serializable decisions: {games} Classic/modern games, {boundaries} boundaries, {checkpoints} quiescent clone resumes; JSON, legacy history, invalid-answer hashes");
    }

    private static Game Fixture(string card, bool history = true)
    {
        var game = new Game(new GameConfig { RandomSeed = 91, StartPlayer = 1, SkipMulligan = true,
            Player1HeroClass = CardClass.DRUID, History = history, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(), FormatType = FormatType.FT_WILD });
        game.StartGame();
        game.Player1.BaseMana = 10;
        Entity.FromCard(game.Player1, Cards.FromId(card), zone: game.Player1.HandZone);
        return game;
    }

    private static void AtomicOptions()
    {
        // Wrath has a branch and target in the same task; no draft/prompt mutates mana or the card.
        var game = Fixture("EX1_154");
        Entity.FromCard(game.Player2, Cards.FromId("CS2_182"), zone: game.Player2.BoardZone);
        var legacy = Fixture("EX1_154");
        Entity.FromCard(legacy.Player2, Cards.FromId("CS2_182"), zone: legacy.Player2.BoardZone);
        string before = Fingerprint(game);
        var view = Views(game).Single();
        var branches = view.Options.Where(o => o.EntityId == game.Player1.HandZone.Last().Id).ToArray();
        Check(branches.Any(o => o.BranchIndex == 1) && branches.Any(o => o.BranchIndex == 2) &&
            branches.All(o => o.Kind == DecisionKind.ChooseOne && o.TargetId != 0), "Choose One plus target atomic options");
        Check(Fingerprint(game) == before, "pre-play views do not mutate");
        Reject(game, new DecisionAnswer(view.Id, view.ControllerId, Array.Empty<int>()), "missing option");
        Reject(game, new DecisionAnswer(view.Id, view.ControllerId, new[] { 0, 0 }), "duplicate option");
        Reject(game, new DecisionAnswer(view.Id, game.Player2.Id, new[] { 0 }), "wrong option controller");
        var task = legacy.Player1.Options().Single(t => t.ChooseOne == 2 && t.Target?.Id == branches.First().TargetId);
        var answer = Answer(view, task);
        Accept(game, answer);
        Check(legacy.Process(task), "legacy Wrath accepted");
        Same(game, legacy);
        Reject(game, answer, "stale atomic option");

        game = Fixture("CS2_029");
        var fireball = Views(game).Single().Options.Where(o => o.EntityId == game.Player1.HandZone.Last().Id).ToArray();
        Check(fireball.Length > 0 && fireball.All(o => o.Kind == DecisionKind.Target && o.TargetId != 0), "normal target option classification");
        // A direct legacy action must also invalidate the previous options ID.
        var old = new DecisionAnswer(Views(game).Single().Id, game.Player1.Id, new[] { 0 });
        game.Process(EndTurnTask.Any(game.Player1));
        Reject(game, old, "legacy path invalidates envelope");

        var noHistory = Fixture("EX1_154", history: false);
        Check(noHistory.GetPendingDecisions().Single().ToJson() == Fixture("EX1_154").GetPendingDecisions().Single().ToJson(),
            "decision IDs and offers independent of history capture");
        var detached = new[] { 0 };
        var immutable = new DecisionAnswer(1, 2, detached);
        detached[0] = 99;
        Check(immutable.SelectedOptionIds[0] == 0 && ((IList<int>)immutable.SelectedOptionIds).IsReadOnly, "answer owns immutable selections");
    }

    private static void ChoiceChains()
    {
        // Real card scripts: Discover, Adapt/after-task, three-stage Kazakus, two-stage dynamic Build-A-Beast.
        foreach (string card in new[] { "UNG_941", "UNG_001", "CFM_621", "ICC_828" })
        {
            var game = Fixture(card);
            var legacy = Fixture(card);
            foreach (var instance in new[] { game, legacy })
            {
                if (card == "CFM_621") instance.Player1.DeckZone.GetAll().ToList().ForEach(c => instance.Player1.DeckZone.Remove(c));
                Check(instance.Process(PlayCardTask.Any(instance.Player1, instance.Player1.HandZone.Last())), "choice fixture play " + card);
                if (card == "ICC_828")
                {
                    instance.Player1.UsedMana = 0;
                    Check(instance.Process(HeroPowerTask.Any(instance.Player1)), "Build-A-Beast start");
                }
            }
            int prompts = 0;
            while (game.Player1.Choice != null)
            {
                AssertQuiescent(game);
                var view = Views(game).Single();
                Check(view.Kind == DecisionKind.GeneralChoice && view.MinSelections == 1 && view.MaxSelections == 1, "general cardinality");
                Check(view.Options.Select(o => o.EntityId).SequenceEqual(game.Player1.Choice.Choices), "offer order preserved");
                int selected = view.Options[prompts % view.Options.Count].EntityId;
                Reject(game, new DecisionAnswer(view.Id, view.ControllerId, Array.Empty<int>()), "missing choice");
                Reject(game, new DecisionAnswer(view.Id, view.ControllerId, new[] { selected, selected }), "duplicate choice");
                var clone = game.CloneExact(history: true);
                var answer = new DecisionAnswer(view.Id, view.ControllerId, new[] { selected });
                Accept(clone, answer);
                Accept(game, answer);
                Check(legacy.Process(ChooseTask.Pick(legacy.Player1, selected)), "legacy choice accepted");
                Same(game, legacy);
                Same(game, clone, fullHistory: false);
                Reject(game, answer, "old chained choice");
                Check(++prompts <= 4, "chain terminates");
            }
            Check(prompts == (card == "ICC_828" ? 2 : card == "CFM_621" ? 3 : 1), "expected choice chain length " + card);
        }
    }

    private static void JsonContract()
    {
        var view = MulliganGame().GetPendingDecisions()[0];
        string json = view.ToJson();
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
            Check(view.ToJson() == json, "JSON independent of culture");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        foreach (string bad in new[] { "null", "{}", json + " trailing", json + "{}", json.Replace("\"Id\":", "\"Id\":9223372036854775808"),
            json.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":99"),
            json.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":1,\"SchemaVersion\":1"),
            json.Replace("\"ControllerId\":" + view.ControllerId, "\"ControllerId\":2147483648") })
        {
            bool rejected = false;
            try { PendingDecision.FromJson(bad); }
            catch (SerializationException) { rejected = true; }
            Check(rejected, "malformed/unsupported view rejected");
        }
        bool unsupported = false;
        try { DecisionAnswer.FromJson(new DecisionAnswer(view.Id, view.ControllerId, Array.Empty<int>()).ToJson()
            .Replace("\"SchemaVersion\":1", "\"SchemaVersion\":99")); }
        catch (SerializationException) { unsupported = true; }
        Check(unsupported, "unsupported answer rejected");
    }

    internal static void SelectorRejectsWithoutMutation()
    {
        var game = PlayerDecisionProof.Fixture(); var p = game.Player1;
        var selected = SabberStoneCore.Actions.Generic.DrawCard(p, Cards.FromId("CORE_BT_801"));
        var invalid = SabberStoneCore.Actions.Generic.DrawCard(p, Cards.FromId("CS2_029"));
        Core2026Proof.Play(game, p, "CATA_697");
        var decision = Views(game).Single();
        Reject(game, new DecisionAnswer(decision.Id + 1, p.Id, new[] { selected.Id }), "selector stale identity");
        Reject(game, new DecisionAnswer(decision.Id, p.Opponent.Id, new[] { selected.Id }), "selector owner");
        Reject(game, new DecisionAnswer(decision.Id, p.Id, new[] { selected.Id, selected.Id }), "selector duplicate");
        Reject(game, new DecisionAnswer(decision.Id, p.Id, new[] { invalid.Id }), "selector unoffered");
        new SabberStoneCore.Tasks.SimpleTasks.TransformTask("CS2_029", EntityType.TARGET).Process(game, p, p.Hero, selected);
        Reject(game, new DecisionAnswer(decision.Id, p.Id, new[] { selected.Id }), "selector school changes while pending");
        p.HandZone.Remove(game.IdEntityDic[selected.Id]); p.SetasideZone.Add(game.IdEntityDic[selected.Id]);
        Reject(game, new DecisionAnswer(decision.Id, p.Id, new[] { selected.Id }), "selector candidate left zone");
        // Cost validation must not warm even an explicitly cold current-cost cache.
        var spec = new SabberStoneCore.Model.Effects.SelectorSpec(SabberStoneCore.Model.Effects.SelectorSource.Hand, maximumCurrentCost: 3);
        var recipe = StateTaskList.Suspendable("proof/selector/cost-reject/v1", new SabberStoneCore.Tasks.SimpleTasks.SelectEntityThenContinueTask(spec));
        var costGame = PlayerDecisionProof.Fixture(); var held = SabberStoneCore.Actions.Generic.DrawCard(costGame.Player1, Cards.FromId("CS2_182")); held.Cost = 2;
        recipe.Process(costGame, costGame.Player1, costGame.Player1.Hero, null);
        held.Cost = 8;
        typeof(Playable).GetField("_modifiedCost", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(held, null);
        var prompt = Views(costGame).Single();
        Reject(costGame, new DecisionAnswer(prompt.Id, prompt.ControllerId, new[] { held.Id }), "selector changed effective cost");
        var boardGame = PlayerDecisionProof.Fixture(); var owner = boardGame.Player1;
        var enemy = Core2026Proof.Summon(owner.Opponent, "CS2_182");
        var board = StateTaskList.Suspendable("proof/selector/board-reject/v1", new SelectEntityThenContinueTask(
            new SabberStoneCore.Model.Effects.SelectorSpec(SabberStoneCore.Model.Effects.SelectorSource.Board,
                SabberStoneCore.Model.Effects.SelectorSide.Enemy)));
        board.Process(boardGame, owner, owner.Hero, null);
        enemy.IsImmune = true;
        typeof(Character).GetField("_modifiedHealth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, null);
        typeof(Character).GetField("_modifiedStealth", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, null);
        var cold = boardGame.SaveSnapshot(); var boardPrompt = Views(boardGame).Single();
        Reject(boardGame, new DecisionAnswer(boardPrompt.Id, owner.Id, new[] { enemy.Id }), "selector board became Immune with cold character caches");
        Check(cold.SequenceEqual(boardGame.SaveSnapshot()), "board rejection preserves cold health/Stealth caches byte-for-byte");
    }

    internal static void Run(bool full)
    {
        Mulligans();
        AtomicOptions();
        ChoiceChains();
        JsonContract();
        SeededGames(full);
        Console.WriteLine("PASS decision focused keep lists, ownership, stale answers, atomic branch/target, Discover/Adapt/Kazakus/Build-A-Beast");
    }
}
