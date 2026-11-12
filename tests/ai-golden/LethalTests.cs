using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

public static class LethalTests
{
    private sealed class SearchJob : INodeJob
    {
        private readonly Stack<int> frontier = new Stack<int>();
        public readonly List<int> Visited = new List<int>();
        public bool IsDone { get { return frontier.Count == 0; } }

        public SearchJob(int root) { frontier.Push(root); }
        public void StepNode()
        {
            int node = frontier.Pop();
            Visited.Add(node);
            if (node >= 16) return;
            frontier.Push(node * 2 + 1);
            frontier.Push(node * 2);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void TestSchedulerSlicing()
    {
        foreach (int cap in new[] { 0, 1, 7, 31, 100 })
        {
            string expected = null;
            foreach (int slice in new[] { 1, 7, 1000 })
            {
                var job = new SearchJob(1);
                var scheduler = new NodeScheduler(cap);
                scheduler.Enqueue(job);
                NodeStepResult state;
                do
                {
                    int before = scheduler.Nodes;
                    state = scheduler.Step(slice);
                    Check(scheduler.Nodes - before <= slice, "Slice exceeded its budget.");
                } while (state == NodeStepResult.Running);
                string actual = string.Join(",", job.Visited) + "/" + scheduler.Nodes + "/" + scheduler.IsBudgetExhausted;
                if (expected == null) expected = actual;
                Check(expected == actual, "Slicing changed search order, count or stop reason.");
                Check(scheduler.Nodes == Math.Min(cap, 31), "Global node budget was not respected.");
                Check(scheduler.IsBudgetExhausted == (cap < 31), "Exact-cap completion was reported as exhaustion.");
                Check(scheduler.Step(slice) == NodeStepResult.Done, "Finished scheduler resumed work.");
                Check(actual == string.Join(",", job.Visited) + "/" + scheduler.Nodes + "/" + scheduler.IsBudgetExhausted,
                    "Finished scheduler mutated a job.");
            }
        }
    }

    public static void TestSchedulerSharedBudget()
    {
        var first = new SearchJob(16);
        var second = new SearchJob(1);
        var scheduler = new NodeScheduler(3);
        scheduler.Enqueue(first);
        scheduler.Enqueue(second);
        Check(scheduler.Step(1000) == NodeStepResult.Done, "Shared budget did not terminate.");
        Check(first.Visited.SequenceEqual(new[] { 16 }) && second.Visited.SequenceEqual(new[] { 1, 2 }),
            "Jobs did not share the budget in FIFO order.");
        Check(scheduler.Nodes == 3 && scheduler.IsBudgetExhausted, "Shared cap accounting failed.");
    }

    public static void TestSchedulerEmptyAndZeroSlice()
    {
        var scheduler = new NodeScheduler(5);
        Check(scheduler.Step(0) == NodeStepResult.Done, "Empty scheduler should be done.");
        var job = new SearchJob(16);
        scheduler.Enqueue(job);
        Check(scheduler.Step(0) == NodeStepResult.Running && scheduler.Nodes == 0, "Zero slice consumed work.");
        Check(scheduler.Step(1) == NodeStepResult.Done && job.IsDone, "Single node did not complete.");
        scheduler.Enqueue(job);
        Check(scheduler.Step(1) == NodeStepResult.Done && scheduler.Nodes == 1, "Completed job consumed budget.");
        bool rejected = false;
        try { scheduler.Step(-1); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected, "Negative slice was accepted.");
        rejected = false;
        try { new NodeScheduler(-1); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected, "Negative cap was accepted.");
    }

    internal static Game Fixture(string kind, int seat = 1)
    {
        // The opening draws consume the prefix; the declared Warptooth stays in its own deck.
        var deck = Enumerable.Repeat("CS2_182", 6).Concat(new[] { "JAIL_421" }).Concat(Enumerable.Repeat("CS2_182", 23)).Select(Cards.FromId).ToList();
        var game = new Game(new GameConfig { Player1HeroClass = CardClass.WARRIOR, Player2HeroClass = CardClass.WARRIOR,
            Player1Deck = deck, Player2Deck = deck.ToList(), StartPlayer = seat, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false, RandomSeed = 7 });
        game.StartGame();
        foreach (var player in new[] { game.Player1, game.Player2 })
        {
            foreach (var card in player.HandZone.ToArray()) { player.HandZone.Remove(card); player.SetasideZone.Add(card); }
            player.BaseMana = 0; player.UsedMana = 0;
        }
        var bot = game.CurrentPlayer;
        if (kind == "fissure" || kind == "near")
        {
            bot.BaseMana = 2;
            var weapon = Entity.FromCard(bot, Cards.FromId("CS2_106"), zone: bot.HandZone);
            weapon.Cost = 0;
            Check(game.Process(PlayCardTask.Any(bot, weapon)), "Weapon fixture failed.");
            Entity.FromCard(bot, Cards.FromId("CATA_582"), zone: bot.HandZone);
            bot.Opponent.Hero.Damage = kind == "near" ? 23 : 24;
        }
        else if (kind == "torch")
        {
            bot.BaseMana = 2;
            Entity.FromCard(bot, Cards.FromId("CATA_585"), zone: bot.HandZone);
            for (int i = 0; i < 2; i++)
            {
                var taunt = (Minion)Entity.FromCard(bot.Opponent, Cards.FromId("CS2_182"), zone: bot.Opponent.BoardZone);
                taunt.HasTaunt = true; taunt.Damage = taunt.Health - 1;
            }
            var attacker = (Minion)Entity.FromCard(bot, Cards.FromId("CS2_182"), zone: bot.BoardZone);
            attacker.IsExhausted = false;
            bot.Opponent.Hero.Damage = 26;
        }
        else if (kind == "warptooth")
        {
            bot.BaseMana = 2;
            Entity.FromCard(bot, Cards.FromId("CATA_582"), zone: bot.HandZone);
            for (int i = 0; i < 4; i++)
            {
                var minion = (Minion)Entity.FromCard(bot, Cards.FromId("CS2_182"), zone: bot.BoardZone);
                minion.IsExhausted = true;
            }
            bot.Hero.IsExhausted = true;
            bot.Opponent.Hero.Damage = 27;
            Check(bot.DeckZone.Any(c => c.Card.Id == "JAIL_421"), "Warptooth fixture drew its own trigger.");
        }
        else if (kind == "volcano")
        {
            bot.BaseMana = 2;
            Entity.FromCard(bot, Cards.FromId("CATA_582"), zone: bot.HandZone);
            Entity.FromCard(bot, Cards.FromId("CATA_584"), zone: bot.BoardZone);
            bot.Hero.IsExhausted = true;
            bot.Opponent.Hero.Damage = 24;
        }
        else if (kind == "random")
        {
            Entity.FromCard(bot, Cards.FromId("CATA_584"), zone: bot.BoardZone);
            Entity.FromCard(bot.Opponent, Cards.FromId("CS2_182"), zone: bot.Opponent.BoardZone);
            bot.Opponent.Hero.Damage = 27;
        }
        game.DeathProcessingAndAuraUpdate();
        return game;
    }

    internal static FairBotState View(Game game, int seed = 83) => FairBotState.Create(game,
        game.CurrentPlayer.PlayerId, seed, DeckStyle.Tempo,
        new FairOptions { TrackOwnDeck = true, MaterializeOwnDeckTriggers = true, RemoveOpponentSecrets = true });

    private static LethalResult Sliced(Game game, FairBotState state, int cap, int slice)
    {
        var job = LethalSolver.Job(state, cap);
        var scheduler = new NodeScheduler(cap);
        scheduler.Enqueue(job);
        while (scheduler.Step(slice) == NodeStepResult.Running) { }
        Check(job.IsDone, "Lethal job did not finish at its own cap.");
        Check(job.Result.Nodes == scheduler.Nodes && job.Result.Nodes <= cap, "Lethal work escaped node accounting.");
        var result = job.Result;
        job.StepNode();
        Check(ReferenceEquals(result, job.Result), "Completed lethal job changed result.");
        return AuthoritativeResult(game, state, result);
    }

    private static LethalResult Find(Game game, FairBotState state, int cap) =>
        AuthoritativeResult(game, state, LethalSolver.Find(state, cap));

    private static LethalResult AuthoritativeResult(Game game, FairBotState state, LethalResult result)
    {
        if (result.Outcome != LethalOutcome.Found) return result;
        var replay = game.CloneExact();
        var bot = replay.CurrentPlayer;
        foreach (var action in result.Line)
        {
            var translated = action;
            // Sampled recruits and newly summoned tokens have canonical ids; replay by their public board slots.
            if (action.Type == PlayerTaskType.MINION_ATTACK
                && state.Facts.Own.Board.Any(c => c.EntityId == action.Source))
            {
                var attacker = state.Facts.Own.Board.Single(c => c.EntityId == action.Source);
                var live = bot.BoardZone[attacker.Position];
                Check(live.Card.Id == attacker.CardId, "Canonical attacker differs from authoritative board slot");
                var target = state.Facts.Opponent.Board.SingleOrDefault(c => c.EntityId == action.Target);
                translated = BotAction.From(MinionAttackTask.Any(bot,
                    live, target == null ? (ICharacter)replay.IdEntityDic[action.Target] : bot.Opponent.BoardZone[target.Position]));
            }
            if (action.Type == PlayerTaskType.PLAY_CARD)
            {
                var held = state.Facts.Own.Hand.Single(c => c.EntityId == action.Source);
                var live = bot.HandZone[held.Position];
                Check(live.Card.Id == held.CardId, "Canonical card differs from authoritative hand slot");
                translated = bot.Options().Select(BotAction.From).Single(a => a.Type == action.Type && a.Source == live.Id
                    && a.Target == action.Target && a.ChooseOne == action.ChooseOne && a.Position == action.Position);
            }
            var legal = bot.Options();
            var task = legal.SingleOrDefault(t => translated.Equals(BotAction.From(t)));
            Check(task != null, "No authoritative action for " + action + "; legal=" + string.Join(";", legal.Select(BotAction.From)));
            Check(replay.Process(task), "Authoritative replay rejected action: " + action);
            state = state.Apply(action);
        }
        Check(bot.PlayState == PlayState.WON, "Found line does not win on authority: " + Trace(result));
        return result;
    }

    private static string Trace(LethalResult result) => result.Outcome + "/" + result.Nodes + "/" + string.Join(";", result.Line);

    private static void Wins(FairBotState state, LethalResult result)
    {
        Check(result.Outcome == LethalOutcome.Found, "Expected lethal, got " + Trace(result));
        foreach (var action in result.Line)
        {
            Check(state.Actions.Contains(action), "Returned line contains illegal action: " + action);
            state = state.Apply(action);
        }
        Check(state.Facts.Own.PlayState == PlayState.WON, "Returned line does not win.");
    }

    public static void TestLethalRequiredLines()
    {
        foreach (string kind in new[] { "fissure", "torch", "warptooth", "volcano" })
        {
            var game = Fixture(kind);
            var before = game.SaveSnapshot();
            var state = View(game);
            var result = Find(game, state, 2000);
            Check(result.Outcome == LethalOutcome.Found, kind + " missing lethal: " + Trace(result));
            Wins(state, result);
            for (int sample = 1; sample <= 4; sample++) Wins(state.WithArtificialSeed(sample), result);
            Check(result.Line.Count >= 2 && result.Line.Count <= 5, kind + " did not exercise a multi-action line.");
            if (kind == "torch") Check(result.Line.Count(a => a.Type == PlayerTaskType.PLAY_CARD) == 2, "Torch excess chain missing.");
            if (kind == "warptooth")
            {
                var child = state.Apply(result.Line[0]);
                Check(child.Facts.DistinctFriendlyCharactersDamagedThisTurn == 4
                    && child.Facts.Own.Board.Any(c => c.CardId == "JAIL_421")
                    && !child.Facts.OwnDeckKnowledge.ContainsKey("JAIL_421"), "Line did not summon canonical own-deck Warptooth.");
            }
            if (kind == "volcano") Check(result.Line[0].Type == PlayerTaskType.PLAY_CARD
                && result.Line[1].Type == PlayerTaskType.ACTIVATE_LOCATION, "Fire must precede Volcano activation.");
            Check(before.SequenceEqual(game.SaveSnapshot()), "Lethal search mutated authority: " + kind);
        }
    }

    public static void TestLethalNearMissAndCaps()
    {
        var game = Fixture("near");
        var near = Find(game, View(game), 2000);
        Check(near.Outcome == LethalOutcome.None && near.Line.Count == 0, "Near miss was not exhausted as None.");
        foreach (int cap in new[] { 0, 1, 2 })
        {
            game = Fixture("fissure");
            var result = Sliced(game, View(game), cap, 1);
            Check(result.Outcome == LethalOutcome.Incomplete && result.Line.Count == 0 && result.Nodes == cap,
                "Tiny cap must report Incomplete, not a partial lethal.");
        }
        bool rejected = false;
        try { LethalSolver.Job(View(Fixture("fissure")), -1); } catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected, "Negative lethal cap accepted.");
        rejected = false;
        try { LethalSolver.Job(null, 1); } catch (ArgumentNullException) { rejected = true; }
        Check(rejected, "Null lethal state accepted.");
    }

    internal static Game HiddenVariant(Game game)
    {
        var copy = game.CloneExact();
        var bot = copy.CurrentPlayer;
        foreach (var old in bot.Opponent.HandZone.ToArray())
            bot.Opponent.HandZone.ChangeEntity(old, Entity.FromCard(bot.Opponent, Cards.FromId("CS2_029"), id: old.Id));
        foreach (var old in bot.Opponent.DeckZone.ToArray())
            bot.Opponent.DeckZone.ChangeEntity(old, Entity.FromCard(bot.Opponent, Cards.FromId("EX1_414"), id: old.Id));
        // Change only a foreign own-deck card; the known own-origin multiset is fixed.
        foreach (var old in bot.DeckZone.Where(c => c[GameTag.DISPLAYED_CREATOR] == bot.Opponent.Hero.Id).ToArray())
            bot.DeckZone.ChangeEntity(old, Entity.FromCard(bot, Cards.FromId("CS2_029"), id: old.Id));
        copy.SetRandomSeed(987654);
        bot.DeckZone.Shuffle(); bot.Opponent.DeckZone.Shuffle();
        return copy;
    }

    public static void TestLethalSlicingAndHiddenVariants()
    {
        foreach (int seat in new[] { 1, 2 })
        foreach (string kind in new[] { "fissure", "torch", "warptooth", "volcano", "near" })
        {
            var game = Fixture(kind, seat);
            Entity.FromCard(game.CurrentPlayer.Opponent, Cards.FromId("CS2_182"), zone: game.CurrentPlayer.Opponent.HandZone);
            game.CurrentPlayer.DeckZone.AddGenerated(Cards.FromId("CS2_182"), game.CurrentPlayer.Opponent.Hero);
            var state = View(game);
            var hidden = HiddenVariant(game);
            var twin = View(hidden);
            Check(state.Describe() == twin.Describe(), "Hidden pair changed public fixture.");
            string expected = Trace(Find(game, state, 2000));
            foreach (int slice in new[] { 1, 7, 1000 })
            {
                Check(Trace(Sliced(game, state, 2000, slice)) == expected, "Slice changed line/count: " + kind);
                Check(Trace(Sliced(hidden, twin, 2000, slice)) == expected, "Hidden variant changed line/count: " + kind);
            }
        }
    }

    private static string ProcessSignature() => string.Join("|", new[] { "fissure", "torch", "warptooth", "volcano", "near", "random" }
        .Select(kind => { var game = Fixture(kind); return kind + ":" + Trace(Find(game, View(game), 2000)); }));

    public static void TestLethalAcrossProcesses()
    {
        const string flag = "HS_LETHAL_TEST_CHILD";
        if (Environment.GetEnvironmentVariable(flag) == "1")
        {
            Console.WriteLine("LETHAL_SIGNATURE=" + ProcessSignature());
            Environment.Exit(0);
        }
        string expected = "LETHAL_SIGNATURE=" + ProcessSignature();
        for (int i = 0; i < 2; i++)
        {
            string executable = Environment.ProcessPath;
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            if (System.IO.Path.GetFileNameWithoutExtension(executable) == "dotnet")
                start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.Environment[flag] = "1";
            using (var process = Process.Start(start))
            {
                var output = process.StandardOutput.ReadToEndAsync();
                var errors = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(60000)) { process.Kill(); throw new Exception("Lethal child timed out."); }
                Check(process.ExitCode == 0, "Lethal child failed: " + errors.GetAwaiter().GetResult());
                Check(output.GetAwaiter().GetResult().Split('\n').Select(s => s.Trim()).Contains(expected), "Process changed lethal results.");
            }
        }
    }

    public static void TestLethalRejectsLuckyRandomLine()
    {
        // Three hits can kill the hero only if all miss the healthy minion. Find a lucky base seed.
        bool exercised = false;
        for (int seed = 1; seed <= 100; seed++)
        {
            var game = Fixture("random");
            var state = View(game, seed);
            var action = state.Actions.Single(a => a.Type == PlayerTaskType.ACTIVATE_LOCATION);
            if (state.Apply(action).Facts.Own.PlayState != PlayState.WON) continue;
            exercised = true;
            var result = Find(game, state, 2000);
            Check(result.Outcome == LethalOutcome.None, "Lucky single-seed Volcano claimed lethal.");
            break;
        }
        Check(exercised, "Random fixture never exercised a lucky winning base seed.");
    }

    public static void TestLethalReplayBudgetAndIsolation()
    {
        var game = Fixture("fissure");
        var state = View(game);
        string before = state.Describe();
        var result = Find(game, state, 2000);
        Wins(state, result);
        Check(result.Nodes >= 1 + 5 * result.Line.Count, "Four sample replays were not charged as nodes.");
        Check(Find(game, state, result.Nodes - 1).Outcome == LethalOutcome.Incomplete,
            "Unfinished fourth sample was accepted as Found.");
        Check(Trace(Sliced(game, state, result.Nodes, 1)) == Trace(result), "Exact-cap success was reported as Incomplete.");
        var job = LethalSolver.Job(state, 2000);
        var scheduler = new NodeScheduler(3);
        scheduler.Enqueue(job);
        scheduler.Step(1000);
        Check(!job.IsDone && job.Result.Outcome == LethalOutcome.Incomplete && job.Result.Nodes == 3,
            "Shared budget cut must retain the continuation and honest result.");
        while (!job.IsDone) job.StepNode();
        Check(Trace(AuthoritativeResult(game, state, job.Result)) == Trace(result), "Resuming shared-budget cut changed result.");
        Check(state.Describe() == before, "Sample replays mutated the start state.");
    }

    public static void TestLethalRejectsUnknownDrawBeforeWinningTrigger()
    {
        foreach (int seat in new[] { 1, 2 })
        {
            var game = Fixture("empty", seat);
            var bot = game.CurrentPlayer;
            bot.BaseMana = 1;
            bot.Hero.Damage = 28;
            bot.Opponent.Hero.Damage = 29;
            foreach (var card in bot.DeckZone.ToArray())
            { bot.DeckZone.Remove(card); bot.SetasideZone.Add(card); }
            bot.DeckZone.AddGenerated(Cards.FromId("JAIL_443t"), bot.Opponent.Hero);
            foreach (string id in new[] { "CORE_ULD_133", "CAP_107t" })
            {
                var minion = (Minion)Entity.FromCard(bot, Cards.FromId(id), zone: bot.BoardZone);
                minion.IsExhausted = true;
            }
            game.DeathProcessingAndAuraUpdate();
            var state = View(game);
            var end = state.Actions.Single(a => a.Type == PlayerTaskType.END_TURN);
            var child = state.Apply(end);
            Check(child.Facts.Own.Hand.Any(c => c.Unknown) && child.Facts.Own.PlayState == PlayState.WON,
                "Fixture must draw a masked card before the simulated winning ping.");
            var authority = game.CloneExact();
            var owner = authority.CurrentPlayer;
            Check(authority.Process(end.Resolve(owner.Options())) && owner.PlayState != PlayState.WON
                && owner.Hero.Health <= 0, "Authoritative Blight must kill the bot before it can win.");
            var result = LethalSolver.Find(state, 100);
            Check(result.Outcome == LethalOutcome.Incomplete && result.Line.Count == 0 && result.Nodes < 100,
                "Unknown draw before a winning trigger must be Incomplete: " + Trace(result));
            AuthoritativeResult(game, state, result);
        }
    }

    public static void TestLethalEndTurnAndUnknownBoundary()
    {
        var game = Fixture("empty");
        var cannon = (Minion)Entity.FromCard(game.CurrentPlayer, Cards.FromId("CAP_107t"), zone: game.CurrentPlayer.BoardZone);
        cannon.IsExhausted = true;
        game.CurrentPlayer.Opponent.Hero.Damage = 29;
        var state = View(game);
        var result = Find(game, state, 100);
        Wins(state, result);
        Check(result.Line.Count == 1 && result.Line[0].Type == PlayerTaskType.END_TURN,
            "Own end-turn Cannoneer lethal was skipped.");
        for (int sample = 1; sample <= 4; sample++) Wins(state.WithArtificialSeed(sample), result);

        game = Fixture("empty");
        game.CurrentPlayer.BaseMana = 3;
        foreach (var card in game.CurrentPlayer.DeckZone.ToArray()) {
            game.CurrentPlayer.DeckZone.Remove(card); game.CurrentPlayer.SetasideZone.Add(card);
        }
        game.CurrentPlayer.DeckZone.AddGenerated(Cards.FromId("CS2_182"), game.CurrentPlayer.Opponent.Hero);
        game.CurrentPlayer.DeckZone.AddGenerated(Cards.FromId("CS2_182"), game.CurrentPlayer.Opponent.Hero);
        Entity.FromCard(game.CurrentPlayer, Cards.FromId("CS2_023"), zone: game.CurrentPlayer.HandZone);
        result = Find(game, View(game), 100);
        Check(result.Outcome == LethalOutcome.Incomplete && result.Nodes < 100,
            "Unknown own draw must not claim exhaustive None or cross the hidden boundary.");
    }

    public static void TestPrivateHistoryBranchDoesNotAbortSearch()
    {
        var game = Fixture("empty");
        var burglar = (Minion)Entity.FromCard(game.CurrentPlayer, Cards.FromId("JAIL_205"), zone: game.CurrentPlayer.BoardZone);
        burglar.IsExhausted = true;
        Entity.FromCard(game.CurrentPlayer.Opponent, Cards.FromId("CS2_182"), zone: game.CurrentPlayer.Opponent.HandZone);
        game.DeathProcessingAndAuraUpdate();
        var state = View(game);
        var before = game.SaveSnapshot();
        var result = LethalSolver.Find(state, 100);
        Check(result.Outcome == LethalOutcome.Incomplete && result.Nodes < 100 && result.Line.Count == 0,
            "Unavailable private history must stop only that branch, without claiming exhaustive None");
        Check(before.SequenceEqual(game.SaveSnapshot()), "Private-history boundary mutated authority");
        var replay = LethalJob.Verify(state, new[] { state.Actions.Single(a => a.Type == PlayerTaskType.END_TURN) }, 100);
        while (!replay.IsDone) replay.StepNode();
        Check(replay.Result.Outcome == LethalOutcome.Incomplete, "Private-history replay cannot verify lethal");
        var planner = TurnPlanner.Job(state, NpcCatalog.All[0], DeckPlans.DragonWarrior, 83);
        while (!planner.IsDone) planner.StepNode();
        Check(planner.Result.Line.Count > 0 && state.Actions.Contains(planner.Result.Line[0]),
            "Private-history end-turn boundary must retain a legal plan");
    }
}
