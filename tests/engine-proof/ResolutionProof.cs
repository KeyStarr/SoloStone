using System.Reflection;
using System.Text.Json;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

internal static class ResolutionProof
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception("resolution: " + message); }
    private static readonly List<object> Reports = new();
    private static readonly MethodInfo ProcessTasks = typeof(Game).GetMethod("ProcessTasks", BindingFlags.Instance | BindingFlags.NonPublic);
    private static void Drain(Game game) => ProcessTasks.Invoke(game, null);
    private static Game NewGame()
    {
        var game = new Game(new GameConfig { RandomSeed = 17, StartPlayer = 1, SkipMulligan = true, History = true, Logging = false });
        game.StartGame();
        game.Player1.BaseMana = 10;
        return game;
    }
    private static IPlayable Owner(Game game, bool enemy = false) => Entity.FromCard(enemy ? game.Player2 : game.Player1,
        Cards.FromId("CS2_182"), zone: enemy ? game.Player2.BoardZone : game.Player1.BoardZone);
    private static Trigger Listen(IPlayable owner, TriggerType type, Action action, bool fast = false) =>
        new Trigger(type) { TriggerSource = TriggerSource.ALL, FastExecution = fast,
            SingleTask = new CustomTask((_, _, _, _, _) => action()) }.Activate(owner, asAncillary: true);
    private static void Raise(Game game, string name, IEntity source) => typeof(TriggerManager)
        .GetMethod("On" + name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(game.TriggerManager, new object[] { source });

    private sealed class Capture
    {
        internal readonly List<ResolutionObservation> Events = new();
        private readonly Stack<ResolutionFrame> _stack = new();
        internal void Observe(ResolutionObservation value)
        {
            if (value.Stage == "start")
            {
                Check(value.Frame.Depth == _stack.Count && value.Frame.ParentId == (_stack.Count == 0 ? 0 : _stack.Peek().Id), "frame parent/depth");
                _stack.Push(value.Frame);
            }
            if (value.Stage == "end") Check(_stack.Pop() == value.Frame, "LIFO frame end");
            Events.Add(value);
        }
        internal void Balanced(Game game)
        {
            Check(_stack.Count == 0 && game.Resolution.Depth == 0, "empty boundary stack");
            Check(game.Resolution.FramesStarted == game.Resolution.FramesEnded, "balanced total frames");
        }
    }

    private static void Report(string name, IEnumerable<ResolutionObservation> events)
    {
        var values = events.Where(e => e.Response.Id != 0).ToArray();
        Reports.Add(new { name, counts = values.Where(e => e.Stage != "resolved" && e.Response.Divergence != ShadowDivergence.None)
            .GroupBy(e => e.Response.Divergence.ToString()).ToDictionary(g => g.Key, g => g.Count()), observations = values });
    }

    private static void Ordering()
    {
        var game = NewGame();
        var older = Owner(game, true);
        var newer = Owner(game);
        var calls = new List<string>();
        Listen(newer, TriggerType.CAST_SPELL, () => calls.Add("cast-newer"));
        Listen(older, TriggerType.PLAY_CARD, () => calls.Add("play-older"));
        var capture = new Capture(); game.Resolution.Trace = capture.Observe;
        var coin = Generic.DrawCard(game.Player1, Cards.FromId("GAME_005"));
        Check(Generic.PlayCard(game.Player1, coin), "D1 coin played");
        Check(calls.SequenceEqual(new[] { "cast-newer", "play-older" }), "D1 legacy categories unchanged");
        Check(capture.Events.Count(e => e.Stage == "order" && e.Response.Divergence == ShadowDivergence.OrderOfPlay) == 1,
            "D1 V2 unified ORDER_OF_PLAY would put older play-card before newer cast-spell");
        var onPlay = capture.Events.Single(e => e.Stage == "start" && e.Frame.Kind == ResolutionKind.OnPlay);
        var spell = capture.Events.Single(e => e.Stage == "start" && e.Frame.Id == onPlay.Frame.ParentId);
        Check(spell.Frame.Kind == ResolutionKind.SpellCast, "play-card / spell-cast / on-play nesting");
        capture.Balanced(game); Report("D1 mixed categories and opposite-controller play order", capture.Events);
    }

    private static void AfterAttack()
    {
        var game = NewGame();
        var attacker = (Minion)Owner(game);
        var owner = Owner(game, true);
        int calls = 0;
        Listen(owner, TriggerType.ATTACK, () => Listen(owner, TriggerType.AFTER_ATTACK, () => calls++), fast: true);
        var capture = new Capture(); game.Resolution.Trace = capture.Observe;
        attacker.IsExhausted = false;
        Check(Generic.AttackBlock(game.Player1, attacker, game.Player2.Hero, false, false), "D3 attack");
        Check(calls == 1, "D3 legacy newly gained after-attack executes");
        Check(capture.Events.Any(e => e.Stage == "queued" && e.Response.Channel == "AfterAttackTrigger" &&
            e.Response.V2Eligible == false && e.Response.Divergence == ShadowDivergence.ActivationNotEligible), "D3 V2 rejects late after-attack activation");
        capture.Balanced(game); Report("D3 late after-attack", capture.Events);
    }

    private static void SummonMembership()
    {
        var game = NewGame();
        var owner = Owner(game);
        var source = (Minion)Owner(game, true);
        int calls = 0;
        var trigger = Listen(owner, TriggerType.AFTER_SUMMON, () => calls++);
        var capture = new Capture(); game.Resolution.Trace = capture.Observe;
        // Explicit metadata fixture: reactivation has a new identity even with the same owner.
        using (game.Resolution.Sequence(ResolutionKind.Summon, game, source))
        {
            trigger.Remove();
            Listen(owner, TriggerType.AFTER_SUMMON, () => calls++);
            Raise(game, "AfterSummonTrigger", source);
            Drain(game);
        }
        Check(calls == 1 && capture.Events.Any(e => e.Stage == "queued" && e.Response.V2Eligible == false &&
            e.Response.Divergence == ShadowDivergence.ActivationNotEligible), "D3 summon reactivation excluded from frozen membership");
        capture.Balanced(game); Report("D3 after-summon reactivation model fixture", capture.Events);

        game = NewGame(); owner = Owner(game); source = (Minion)Owner(game, true);
        Listen(owner, TriggerType.AFTER_SUMMON, () => { });
        capture = new Capture(); game.Resolution.Trace = capture.Observe;
        using (game.Resolution.Sequence(ResolutionKind.Summon, game, source))
        {
            owner.Controller = game.Player2;
            Raise(game, "AfterSummonTrigger", source); Drain(game);
        }
        Check(capture.Events.Any(e => e.Stage == "queued" && e.Response.Divergence == ShadowDivergence.ControllerChanged &&
            e.Response.V2Eligible == false), "D3 controller membership captured at sequence start");
        capture.Balanced(game); Report("D3 changed-controller model fixture", capture.Events);
    }

    private static void NestedSummonMembership()
    {
        var game = NewGame(); var owner = Owner(game); var late = Owner(game);
        var source = (Minion)Owner(game, true);
        int calls = 0;
        Listen(owner, TriggerType.AFTER_SUMMON, () => calls++);
        var capture = new Capture(); game.Resolution.Trace = capture.Observe;
        using (game.Resolution.Sequence(ResolutionKind.Summon, game, source))
        {
            Listen(late, TriggerType.AFTER_SUMMON, () => calls++);
            using (game.Resolution.Sequence(ResolutionKind.Summon, game, source))
            {
                Raise(game, "AfterSummonTrigger", source); Drain(game);
            }
            Raise(game, "AfterSummonTrigger", source); Drain(game);
        }
        var queued = capture.Events.Where(e => e.Stage == "queued").ToArray();
        Check(calls == 4 && queued.Length == 4 && queued.Select(e => e.Response.V2Eligible).SequenceEqual(new bool?[] { true, true, true, false }),
            "inner summon captures new activation without changing outer membership");
        Check(queued[0].Response.SequenceId == queued[1].Response.SequenceId && queued[2].Response.SequenceId == queued[3].Response.SequenceId &&
            queued[0].Response.SequenceId != queued[2].Response.SequenceId, "nested summon identity");
        capture.Balanced(game); Report("D3 nested summon membership model fixture", capture.Events);
    }

    private static void NestedValidation()
    {
        var game = NewGame();
        var owner = Owner(game);
        int calls = 0; bool nested = false;
        Listen(owner, TriggerType.AFTER_CAST, () => calls++);
        Listen(owner, TriggerType.CAST_SPELL, () => {
            if (nested) return;
            nested = true;
            var inner = Generic.DrawCard(game.Player1, Cards.FromId("GAME_005"));
            Check(Generic.PlayCard(game.Player1, inner), "nested coin played");
        }, fast: true);
        var capture = new Capture(); game.Resolution.Trace = capture.Observe;
        var outer = Generic.DrawCard(game.Player1, Cards.FromId("GAME_005"));
        Check(Generic.PlayCard(game.Player1, outer), "outer coin played");
        Check(calls == 1, "legacy nested validation consumes shared bit");
        var afters = capture.Events.Where(e => e.Response.Channel == "AfterCastTrigger" && (e.Stage == "queued" || e.Stage == "candidate")).ToArray();
        Check(afters.Length == 2 && afters.All(e => e.Response.V2Eligible == true) && afters.Select(e => e.Response.SequenceId).Distinct().Count() == 2,
            "V2 retains distinct eligible outer and inner after-cast frames");
        Check(afters.Count(e => !e.LegacyQueued && e.Response.Divergence == ShadowDivergence.NestedValidation) == 1,
            "D3 outer loss recorded without changing execution");
        capture.Balanced(game); Report("D3 real nested cast consumes outer validation", capture.Events);
    }

    private static void FrozenQueueAndExceptions()
    {
        var game = NewGame(); var owner = Owner(game);
        int calls = 0;
        var trigger = Listen(owner, TriggerType.DRAW, () => calls++);
        var capture = new Capture(); game.Resolution.Trace = capture.Observe;
        Raise(game, "DrawTrigger", game.Player1.Hero);
        trigger.Remove();
        Listen(owner, TriggerType.DRAW, () => calls += 100);
        Drain(game);
        Check(calls == 1, "queued response frozen across removal/new activation");
        Check(capture.Events.Single(e => e.Stage == "queued").Response == capture.Events.Single(e => e.Stage == "resolved").Response,
            "queue provenance retained until execution without rechecking");
        capture.Balanced(game);
        foreach (bool fast in new[] { false, true })
        {
            game = NewGame(); owner = Owner(game); capture = new Capture(); game.Resolution.Trace = capture.Observe;
            Listen(owner, TriggerType.PLAY_CARD, () => throw new InvalidOperationException("fixture"), fast);
            var card = Generic.DrawCard(game.Player1, Cards.FromId("GAME_005"));
            bool threw = false;
            try { Generic.PlayCard(game.Player1, card); }
            catch (Exception) { threw = true; }
            Check(threw, "injected task failure"); capture.Balanced(game);
        }
        game = NewGame(); capture = new Capture(); game.Resolution.Trace = capture.Observe;
        var invalid = Entity.FromCard(game.Player1, Cards.FromId("CS2_182"), zone: game.Player1.HandZone);
        game.Player1.BaseMana = 0;
        Check(!Generic.PlayCard(game.Player1, invalid), "early rejected play"); capture.Balanced(game);
    }

    private static void RecipeStructureAndPredicates()
    {
        var game = NewGame(); var capture = new Capture(); game.Resolution.Trace = capture.Observe;
        var hunter = Generic.DrawCard(game.Player1, Cards.FromId("CS2_196"));
        Check(Generic.PlayCard(game.Player1, hunter), "summoning battlecry played");
        var frames = capture.Events.Where(e => e.Stage == "start").Select(e => e.Frame).ToDictionary(f => f.Id);
        var summon = frames.Values.Single(f => f.Kind == ResolutionKind.Summon);
        var path = new List<ResolutionKind>();
        for (var frame = summon; frame.Id != 0; frame = frame.ParentId == 0 ? default : frames[frame.ParentId]) path.Add(frame.Kind);
        Check(path.SequenceEqual(new[] { ResolutionKind.Summon, ResolutionKind.TaskResolution, ResolutionKind.TaskDrain,
            ResolutionKind.Battlecry, ResolutionKind.PlayMinion, ResolutionKind.PlayCard }), "real battlecry nested summon structure");
        Check(frames.Values.Any(f => f.Kind == ResolutionKind.AuraUpdate && frames[f.ParentId].Kind == ResolutionKind.SummonOn),
            "summon immediate aura pass stays inside insertion phase");
        hunter.ToBeDestroyed = true; game.DeathProcessingAndAuraUpdate();
        Check(capture.Events.Any(e => e.Frame.Kind == ResolutionKind.DeathCreation) && capture.Events.Any(e => e.Frame.Kind == ResolutionKind.DeathResolution),
            "death creation/resolution observed");
        capture.Balanced(game);

        game = NewGame(); var owner = Owner(game); int predicates = 0, calls = 0;
        var definition = new Trigger(TriggerType.PLAY_CARD) { TriggerSource = TriggerSource.ALL,
            Condition = new SelfCondition(_ => { predicates++; return true; }),
            SingleTask = new CustomTask((_, _, _, _, _) => calls++) };
        definition.Activate(owner);
        Check(Generic.PlayCard(game.Player1, Generic.DrawCard(game.Player1, Cards.FromId("GAME_005"))), "predicate coin");
        Check(predicates == 1 && calls == 1, "shadow never reevaluates side-effecting conditions");
    }

    private static void RoundTrips()
    {
        var game = NewGame();
        Generic.SummonBlock(game, (Minion)Entity.FromCard(game.Player1, Cards.FromId("NEW1_019")), -1, null);
        game.DeathProcessingAndAuraUpdate();
        game.Resolution.Trace = _ => throw new Exception("observer must not be copied");
        var clone = game.CloneExact(history: true);
        byte[] bytes = game.SaveSnapshot(); var loaded = Game.LoadSnapshot(bytes);
        Check(bytes.SequenceEqual(loaded.SaveSnapshot()), "snapshot bytes stable with empty stack");
        foreach (var copy in new[] { clone, loaded })
        {
            Check(copy.Resolution.Depth == 0 && copy.Resolution.Trace == null && copy.Resolution.FramesStarted == game.Resolution.FramesStarted,
                "clone/snapshot counters preserved; observer excluded");
        }
        game.Resolution.Trace = null;
        var sourceEvents = new Capture(); var cloneEvents = new Capture(); var loadedEvents = new Capture();
        game.Resolution.Trace = sourceEvents.Observe; clone.Resolution.Trace = cloneEvents.Observe; loaded.Resolution.Trace = loadedEvents.Observe;
        foreach (var copy in new[] { game, clone, loaded })
            Generic.SummonBlock(copy, (Minion)Entity.FromCard(copy.Player1, Cards.FromId("CS2_182")), -1, null);
        Check(sourceEvents.Events.SequenceEqual(cloneEvents.Events) && sourceEvents.Events.SequenceEqual(loadedEvents.Events), "future frame/response IDs survive clone and snapshot");
        foreach (var copy in new[] { game, clone, loaded }) copy.DeathProcessingAndAuraUpdate();
        using (game.Resolution.Enter(ResolutionKind.SpellText))
        {
            bool cloneRejected = false, snapshotRejected = false;
            try { game.CloneExact(); } catch (NotSupportedException) { cloneRejected = true; }
            try { game.SaveSnapshot(); } catch (NotSupportedException) { snapshotRejected = true; }
            Check(cloneRejected && snapshotRejected, "in-flight frames cannot silently disappear in clone/snapshot");
        }
        sourceEvents.Balanced(game);
    }

    internal static void Run(bool full)
    {
        Reports.Clear(); Ordering(); AfterAttack(); SummonMembership(); NestedSummonMembership(); NestedValidation(); FrozenQueueAndExceptions(); RecipeStructureAndPredicates(); RoundTrips();
        int games = 0; long frames = 0;
        foreach (string suite in new[] { "classic", "modern", "core-reprints", "core-2026" })
        {
            var decks = Simulation.DeckIds(suite).Where(d => suite != "core-reprints" || !d.StartsWith("core-2026-")).ToArray();
            for (int seed = 0; seed < (full ? 5 : 1); seed++)
            for (int i = 0; i < decks.Length; i++)
            {
                var capture = new Capture(); Game observed = null;
                var record = new GameRecord { Suite = suite, Player = decks[i], Opponent = decks[(i + 1) % decks.Length], Seed = seed };
                var game = Simulation.Run(record, started: g => { observed = g; g.Resolution.Trace = capture.Observe; },
                    processed: _ => capture.Balanced(observed));
                capture.Balanced(game); games++; frames += game.Resolution.FramesStarted;
                Report($"{suite}/{decks[i]}/{seed}", capture.Events);
            }
        }
        Directory.CreateDirectory("sim/out");
        File.WriteAllText("sim/out/resolution-shadow.json", JsonSerializer.Serialize(Reports,
            new JsonSerializerOptions { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } }));
        Console.WriteLine($"PASS resolution frames: {games} seeded games across four suites; {frames} balanced frames; D1/D3, frozen queue, exceptions, clone/snapshot; report sim/out/resolution-shadow.json");
    }
}
