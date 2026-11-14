using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class ForestLordCenariusProof
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception("Cenarius: " + message); }
    private static Util.DeepCloneableRandom Rng(Game game) => (Util.DeepCloneableRandom)typeof(Game)
        .GetProperty("Random", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);

    internal static Game Fixture(int seed, int friendly, out int sourceId, bool juggler = false, bool dormant = false)
    {
        var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, SkipMulligan = true,
            Player1HeroClass = CardClass.DRUID, History = true, Logging = false,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(), FormatType = FormatType.FT_STANDARD });
        game.StartGame();
        for (int i = 0; i < friendly; i++) Core2026Proof.Summon(game.Player1, "CS2_231");
        if (dormant) game.Player1.BoardZone[0][GameTag.DORMANT] = 1;
        Core2026Proof.Summon(game.Player2, "CS2_231");
        if (juggler) Core2026Proof.Summon(game.Player1, "NEW1_019");
        game.Player1.BaseMana = 10;
        var card = Generic.DrawCard(game.Player1, Cards.FromId("EDR_209"));
        sourceId = card.Id;
        var rng = Rng(game).CaptureState();
        Check(game.Process(PlayCardTask.Any(game.Player1, card)), "legal play");
        Check(Rng(game).DrawCount == rng.DrawCount, "choices defer source summon triggers and consume no RNG on play");
        Check(game.Player1.UsedMana == 10, "pay once");
        return game;
    }

    private static void Same(Game game, Game copy, bool history)
    {
        Check(Canonical.State(game.CloneExact()) == Canonical.State(copy.CloneExact()), "state divergence");
        Check(Rng(game).CaptureState().Equals(Rng(copy).CaptureState()), "RNG divergence");
        Check(game.GetPendingDecisions().Select(v => v.ToJson()).SequenceEqual(copy.GetPendingDecisions().Select(v => v.ToJson())), "view divergence");
        if (history) Check(Canonical.History(game) == Canonical.History(copy), "history divergence");
    }
    private static void Reject(Game game, DecisionAnswer answer)
    {
        var bytes = game.SaveSnapshot();
        Check(!game.TryAnswer(answer, out _), "bad answer accepted");
        Check(bytes.SequenceEqual(game.SaveSnapshot()), "bad answer mutated state/history/RNG");
    }
    private static void AssertBoard(Game game, int sourceId, List<(int Attack, int Health, bool Ancient)> expected)
    {
        var source = (Minion)game.IdEntityDic[sourceId];
        Check(source.AttackDamage == 5 && source.Health == 8, "other excludes Cenarius");
        var other = game.Player1.BoardZone.Where(m => m.Id != sourceId).ToArray();
        Check(other.Length == expected.Count, "board count");
        for (int i = 0; i < other.Length; i++)
            Check(other[i].AttackDamage == expected[i].Attack && other[i].Health == expected[i].Health
                && other[i].HasTaunt == expected[i].Ancient
                && other[i].Card.Id == (expected[i].Ancient ? "EDR_209t5" : "CS2_231"), "branch stats/identity/taunt/order");
        Check(game.Player2.BoardZone.Single().AttackDamage == 1 && game.Player2.BoardZone.Single().Health == 1, "enemy unchanged");
    }
    private static void Branch(int seed, int friendly, int mask)
    {
        var game = Fixture(seed, friendly, out int sourceId);
        var expected = Enumerable.Range(0, friendly).Select(_ => (Attack: 1, Health: 1, Ancient: false)).ToList();
        var oldAnswers = new List<DecisionAnswer>();
        var rng = Rng(game).CaptureState();
        for (int stage = 0; stage < 3; stage++)
        {
            var view = game.GetPendingDecisions().Single();
            Check(view.Kind == DecisionKind.SuspendedEffect && view.ControllerId == game.Player1.Id
                && view.SourceId == sourceId && view.MinSelections == 1 && view.MaxSelections == 1, "pending decision identity/cardinality");
            Check(view.Options.Select(o => game.IdEntityDic[o.EntityId].Card.Id).SequenceEqual(new[] { "EDR_209a", "EDR_209b" }), "fixed ordered choices");
            Check(view.Options.All(o => o.OptionId == o.EntityId && o.Kind == DecisionKind.SuspendedEffect), "entity-backed choices");
            Check(PendingDecision.FromJson(view.ToJson()).ToJson() == view.ToJson(), "decision JSON");
            var bytes = game.SaveSnapshot();
            var restored = Game.LoadSnapshot(bytes);
            var clone = game.CloneExact(history: true);
            Check(bytes.SequenceEqual(restored.SaveSnapshot()), "snapshot byte stability");
            Same(game, restored, true); Same(game, clone, false);
            Check(bytes.SequenceEqual(game.SaveSnapshot()), "clone/save changed source");
            foreach (var stale in oldAnswers) Reject(game, stale);
            Reject(game, new DecisionAnswer(view.Id, game.Player2.Id, new[] { view.Options[0].OptionId }));
            Reject(game, new DecisionAnswer(view.Id + 999, view.ControllerId, new[] { view.Options[0].OptionId }));
            Reject(game, new DecisionAnswer(view.Id, view.ControllerId, Array.Empty<int>()));
            Reject(game, new DecisionAnswer(view.Id, view.ControllerId, new[] { sourceId }));
            Reject(game, new DecisionAnswer(view.Id, view.ControllerId, new[] { view.Options[0].OptionId, view.Options[0].OptionId }));
            if (oldAnswers.Count > 0) Reject(game, new DecisionAnswer(view.Id, view.ControllerId, oldAnswers[0].SelectedOptionIds));
            foreach (var task in new PlayerTask[] { EndTurnTask.Any(game.Player1),
                PlayCardTask.Any(game.Player1, game.Player1.HandZone[0]),
                HeroPowerTask.Any(game.Player1), ChooseTask.Pick(game.Player1, view.Options[0].OptionId) })
            {
                Check(!game.Process(task), "normal/legacy action while suspended");
                Check(bytes.SequenceEqual(game.SaveSnapshot()), "blocked action mutated bytes");
            }
            int branch = (mask >> stage) & 1;
            var answer = new DecisionAnswer(view.Id, view.ControllerId, new[] { view.Options[branch].OptionId });
            foreach (var candidate in new[] { game, restored, clone })
                Check(candidate.TryAnswer(answer, out var error), "resume: " + error);
            Same(game, restored, true); Same(game, clone, false);
            Check(Canonical.Value(game.PowerHistory.Last) == Canonical.Value(clone.PowerHistory.Last), "clone history suffix");
            Check(Rng(game).CaptureState().Equals(rng), "fixed choices consumed RNG");
            oldAnswers.Add(answer);
            Reject(game, answer);
            if (branch == 0) expected = expected.Select(m => (m.Attack + 1, m.Health + 3, m.Ancient)).ToList();
            else if (expected.Count < 6) expected.Add((5, 5, true));
            AssertBoard(game, sourceId, expected);
            Check(game.Player1.UsedMana == 10, "resume charged mana again");
        }
        Check(!game.Resolution.HasSuspendedEffect && game.Player1.Choice == null, "completed exactly three choices");
        Check(game.Resolution.Depth == 0 && game.Resolution.FramesStarted == game.Resolution.FramesEnded, "balanced frames");
        Check(game.Process(EndTurnTask.Any(game.Player1)), "normal play resumes");
    }
    private static void DormantMinion()
    {
        var game = Fixture(37, 1, out _, dormant: true);
        for (int i = 0; i < 3; i++)
        {
            var view = game.GetPendingDecisions().Single();
            Check(game.TryAnswer(new DecisionAnswer(view.Id, view.ControllerId, new[] { view.Options[0].OptionId }), out _), "dormant board buff");
            var sleeper = game.Player1.BoardZone[0];
            Check(sleeper.IsDormant && sleeper.AttackDamage == 1 && sleeper.Health == 1, "dormant minion excluded from buff");
        }
    }

    private static void ConcedeAtPrompts()
    {
        for (int stage = 0; stage < 3; stage++)
        foreach (bool ownerConcedes in new[] { true, false })
        {
            var game = Fixture(71, 1, out _);
            for (int i = 0; i < stage; i++)
            {
                var prompt = game.GetPendingDecisions().Single();
                Check(game.TryAnswer(new DecisionAnswer(prompt.Id, prompt.ControllerId,
                    new[] { prompt.Options[i % 2].OptionId }), out _), "advance concede fixture");
            }
            var view = game.GetPendingDecisions().Single();
            var stale = new DecisionAnswer(view.Id, view.ControllerId, new[] { view.Options[1].OptionId });
            var rng = Rng(game).CaptureState();
            string board = Canonical.Value(game.Player1.BoardZone.Select(m => new { m.Id, m.AttackDamage, m.Health }));
            var suspendedBytes = game.SaveSnapshot();
            var restored = Game.LoadSnapshot(suspendedBytes);
            var clone = game.CloneExact(history: true);
            foreach (var candidate in new[] { game, restored, clone })
            {
                var loser = ownerConcedes ? candidate.Player1 : candidate.Player2;
                Check(candidate.Process(ConcedeTask.Any(loser)), "concede accepted at prompt " + stage);
                Check(candidate.State == State.COMPLETE && loser.PlayState == PlayState.LOST &&
                    (ownerConcedes ? candidate.Player2 : candidate.Player1).PlayState == PlayState.WON, "terminal winner");
                Check(!candidate.Resolution.HasSuspendedEffect && candidate.Player1.Choice == null &&
                    candidate.Player2.Choice == null && candidate.GetPendingDecisions().Count == 0, "no cancelled decision");
                Check(((System.Collections.ICollection)typeof(SabberStoneCore.Model.Events.ResolutionScheduler)
                    .GetField("_effects", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(candidate.Resolution)).Count == 0,
                    "discard entire continuation stack");
                Check(candidate.Resolution.Depth == 0 && candidate.Resolution.FramesStarted == candidate.Resolution.FramesEnded,
                    "concede balances frames");
                Check(Rng(candidate).CaptureState().Equals(rng), "concede consumes no RNG");
                Check(board == Canonical.Value(candidate.Player1.BoardZone.Select(m => new { m.Id, m.AttackDamage, m.Health })),
                    "concede never executes remaining branches");
                string before = Canonical.Trace(candidate);
                Check(!candidate.TryAnswer(stale, out _) && before == Canonical.Trace(candidate), "cancelled answer cannot resume");
                var terminalClone = candidate.CloneExact(history: true);
                Check(!terminalClone.Resolution.HasSuspendedEffect && terminalClone.Player1.Choice == null &&
                    terminalClone.Player2.Choice == null && terminalClone.Resolution.Depth == 0 &&
                    terminalClone.Resolution.FramesStarted == terminalClone.Resolution.FramesEnded, "terminal clone has no continuation");
                Same(candidate, terminalClone, false);
            }
            Same(game, restored, true); Same(game, clone, false);

            // Cancellation is concession-only; a chosen Ancient can still await summon cleanup.
            var cancelled = Game.LoadSnapshot(suspendedBytes);
            typeof(SabberStoneCore.Model.Events.ResolutionScheduler).GetMethod("CancelSuspendedEffect",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cancelled.Resolution, new object[] { cancelled });
            Check(!cancelled.Resolution.HasSuspendedEffect && cancelled.Player1.Choice == null &&
                !Generic.HasDecisionPlay(cancelled.Player1), "cancellation discards recipe and deferred play completion");
            if (cancelled.SummonedMinions.Count != 0)
            {
                bool rejected = false;
                try { cancelled.SaveSnapshot(); }
                catch (NotSupportedException) { rejected = true; }
                Check(rejected, "cancelled in-flight summon cannot be saved as a running boundary");
            }
            else
            {
                var bytes = cancelled.SaveSnapshot();
                var saved = Game.LoadSnapshot(bytes);
                Check(bytes.SequenceEqual(saved.SaveSnapshot()), "cancelled settled boundary snapshot stable");
                Same(cancelled, saved, true);
            }
        }
        Console.WriteLine("PASS Cenarius concede: all three prompts, either player, no continuation/RNG, balanced frames, snapshot/clone parity");
    }

    private static void SummonReactions()
    {
        var game = Fixture(117, 0, out _, juggler: true);
        // Keep both enemy targets alive so every deferred Juggler reaction has exactly one RNG draw.
        game.Player2.BoardZone.Single()[GameTag.HEALTH] = 100;
        for (int stage = 0; stage < 3; stage++)
        {
            var view = game.GetPendingDecisions().Single();
            var restored = Game.LoadSnapshot(game.SaveSnapshot());
            var clone = game.CloneExact(history: true);
            long draws = Rng(game).DrawCount;
            int reactions = stage == 2 ? 2 : 1;
            int health = game.Player2.Hero.Health + game.Player2.BoardZone.Sum(m => m.Health);
            var answer = new DecisionAnswer(view.Id, view.ControllerId, new[] { view.Options[1].OptionId });
            foreach (var candidate in new[] { game, restored, clone })
                Check(candidate.TryAnswer(answer, out _), "summon reaction resume");
            Check(Rng(game).DrawCount == draws + reactions, "one RNG draw per Ancient and deferred source summon, never replayed");
            Check(game.Player2.Hero.Health + game.Player2.BoardZone.Sum(m => m.Health) == health - reactions, "one Juggler damage per Ancient plus source after final choice");
            Same(game, restored, true); Same(game, clone, false);
            Reject(game, answer);
        }
    }

    private static string Future(Game game)
    {
        while (game.Resolution.HasSuspendedEffect)
        {
            var view = game.GetPendingDecisions().Single();
            Check(game.TryAnswer(new DecisionAnswer(view.Id, view.ControllerId, new[] { view.Options[1].OptionId }), out _), "fresh future answer");
        }
        return Canonical.Sha(Canonical.State(game.CloneExact()) + Canonical.History(game) + Canonical.Value(Rng(game).CaptureState()));
    }

    internal static void Child(string directory)
    {
        foreach (var file in Directory.GetFiles(directory, "*.snapshot"))
        {
            var bytes = File.ReadAllBytes(file);
            var game = Game.LoadSnapshot(bytes);
            Check(bytes.SequenceEqual(game.SaveSnapshot()), "fresh process bytes");
            Check(Future(game) == File.ReadAllText(file + ".hash"), "fresh process continuation");
        }
        Console.WriteLine("PASS Cenarius fresh-process restore at all three prompts");
    }

    private static void FreshProcess()
    {
        string directory = Path.Combine(Path.GetTempPath(), "hs-cenarius-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var game = Fixture(71, 1, out _);
            for (int stage = 0; stage < 3; stage++)
            {
                string file = Path.Combine(directory, stage + ".snapshot");
                var bytes = game.SaveSnapshot();
                File.WriteAllBytes(file, bytes);
                File.WriteAllText(file + ".hash", Future(Game.LoadSnapshot(bytes)));
                var view = game.GetPendingDecisions().Single();
                Check(game.TryAnswer(new DecisionAnswer(view.Id, view.ControllerId, new[] { view.Options[1].OptionId }), out _), "advance fresh fixture");
            }
            var start = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath) { RedirectStandardOutput = true, RedirectStandardError = true };
            if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet") start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("--cenarius-child"); start.ArgumentList.Add(directory);
            using var child = System.Diagnostics.Process.Start(start);
            string output = child.StandardOutput.ReadToEnd(), error = child.StandardError.ReadToEnd();
            Check(child.WaitForExit(30000) && child.ExitCode == 0, "fresh process: " + output + error);
            Console.Write(output);
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void Goldens()
    {
        using var data = System.Text.Json.JsonDocument.Parse(File.ReadAllText("tests/goldens/standard-spike1.json"));
        Check(data.RootElement.GetProperty("schema").GetInt32() == 2, "legacy golden schema");
        int plays = 0, answers = 0;
        foreach (var row in data.RootElement.GetProperty("games").EnumerateArray())
        {
            var expected = System.Text.Json.JsonSerializer.Deserialize<GameRecord>(row.GetRawText(),
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            expected.ReplaySchema = 2;
            var game = Simulation.Run(expected, replay: true, processed: task => {
                if (task.PlayerTaskType == PlayerTaskType.PLAY_CARD && task.Source?.Card.Id == "EDR_209") plays++;
                if (task is ChooseTask choice && choice.Controller.Choice?.SourceId is int source &&
                    choice.Controller.Game.IdEntityDic[source].Card.Id == "EDR_209") answers++;
            });
            var actual = new GameRecord();
            Simulation.Finish(actual, game);
            Check(actual.StateSha == expected.StateSha && expected.DecisionsSha != null && actual.DecisionsSha == expected.DecisionsSha &&
                actual.Winner == expected.Winner && actual.Turns == expected.Turns, "Standard spike decisions/state/action golden");
        }
        Check(plays > 0 && answers > 0, "goldens exercise Cenarius and sequential choices");
        Console.WriteLine("PASS Standard spike 1 goldens: 3 replays, Cenarius plays=" + plays);
    }

    internal static void RunStandard() => StandardSetsProof.Case("EDR_209", (g, p, id) => {
        Check(StandardSets.CollectibleIds.Contains(id), "Standard stress corpus includes Cenarius");
        Branch(1, 1, 2);
    });

    internal static void Run()
    {
        var card = Cards.All.FirstOrDefault(c => c.Id == "EDR_209");
        Check(card != null && card.AssetId == 113588 && card.Implemented && EffectModules.ByDbfId.ContainsKey(113588), "registered EDR_209 module");
        Check(card.Cost == 10 && card.ATK == 5 && card.Health == 8, "pinned stats");
        foreach (int seed in new[] { 1, 17, 91 })
        foreach (int friendly in new[] { 0, 1, 5, 6 })
        for (int mask = 0; mask < 8; mask++) Branch(seed, friendly, mask);
        DormantMinion();
        SummonReactions();
        ConcedeAtPrompts();
        FreshProcess();
        Goldens();
        PoolProofEvidence.Passed("EDR_209", "ForestLordCenariusProof.Run");
        Console.WriteLine("PASS Cenarius: 96 branches, 288 prompt clone/snapshot boundaries, rejection byte identity, no RNG draws, ordered buffs/summons/full board");
    }
}
