using System.Collections;
using System.Reflection;
using HsSim;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using Type = System.Type;

internal static partial class RewindProof
{
    // Independent inventory of ControllerAuraEffects' observable tags, including its alias.
    private static readonly GameTag[] ControllerAuraTags = {
        GameTag.TIMEOUT, GameTag.SPELLPOWER_DOUBLE, GameTag.SPELL_HEALING_DOUBLE,
        GameTag.HERO_POWER_DOUBLE, GameTag.HEALING_DOES_DAMAGE, GameTag.CHOOSE_BOTH,
        GameTag.SPELLS_COST_HEALTH, GameTag.EXTRA_BATTLECRIES_BASE, GameTag.EXTRA_END_TURN_EFFECT,
        GameTag.HERO_POWER_DISABLED, GameTag.ALL_HEALING_DOUBLE, GameTag.EXTRA_MINION_BATTLECRIES_BASE,
        GameTag.SPELLPOWER
    };

    private static Game RuntimeFixture(int seed = 19, bool logging = false)
    {
        var g = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, SkipMulligan = true,
            History = true, Logging = logging, ReplayPools = PinnedPoolFixture.Manifest, Player1HeroClass = CardClass.DRUID,
            Player2HeroClass = CardClass.DRUID,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        g.StartGame();
        foreach (var p in new[] { g.Player1, g.Player2 })
        {
            foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
            p.BaseMana = 10;
            p.Hero[GameTag.HEALTH] = 1000;
        }
        return g;
    }

    private static void AuraHistoryRebinding()
    {
        foreach (int owner in new[] { 2, 3 })
        foreach (var (card, tag) in new[] { ("LOE_077", GameTag.EXTRA_BATTLECRIES_BASE),
            ("OG_044", GameTag.CHOOSE_BOTH), ("EX1_350", GameTag.SPELLPOWER_DOUBLE) })
        {
            var g = RuntimeFixture(); Play(g, g.Player1, "TIME_001"); Answer(g, true);
            var games = new[] { g, g.CloneExact(history: true), PinnedPoolFixture.LoadSnapshot(g.SaveSnapshot()) };
            var streams = new List<string>();
            foreach (var game in games)
            {
                game.PowerHistory.Last.Clear();
                var p = game.ControllerById(owner);
                var minion = Summon(p, card);
                Check(p[tag] == 1 && game.PowerHistory.Last.OfType<PowerHistoryTagChange>()
                    .Count(e => e.EntityId == owner && e.Tag == tag && e.Value == 1) == 1,
                    $"AuraHistoryRebinding activation: {card}, controller={owner}");
                Kill(game, minion);
                Check(p[tag] == 0 && game.PowerHistory.Last.OfType<PowerHistoryTagChange>()
                    .Count(e => e.EntityId == owner && e.Tag == tag && e.Value == 0) == 1,
                    $"AuraHistoryRebinding removal: {card}, controller={owner}");
                streams.Add(Canonical.Value(game.PowerHistory.Last));
            }
            Check(streams.Distinct().Count() == 1, "AuraHistoryRebinding source/load/clone entry stream");
        }
        var replay = RuntimeFixture();
        var brann = Add(replay.Player1, "LOE_077"); brann[GameTag.REWIND] = 1;
        Check(replay.Process(PlayCardTask.Any(replay.Player1, brann)), "AuraHistoryRebinding Rewind-enabled Brann play");
        Answer(replay, true);
        Check(replay.PowerHistory.Last.OfType<PowerHistoryTagChange>().Count(e => e.EntityId == replay.Player1.Id &&
            e.Tag == GameTag.EXTRA_BATTLECRIES_BASE && e.Value == 1) == 1,
            "AuraHistoryRebinding callback rebound before replay aura activation");
        Console.WriteLine("PASS Rewind AuraHistoryRebinding: both controllers; Brann/Fandral/Velen activation+removal; source/load/clone history parity");
    }

    private static void EffectiveControllerReset()
    {
        var g = RuntimeFixture();
        foreach (var p in new[] { g.Player1, g.Player2 })
        {
            Summon(p, "OG_044"); Summon(p, "LOE_077"); Summon(p, "CS2_142"); Summon(p, "EX1_350");
            // Exercise every controller aura slot independently of installed card coverage.
            foreach (var tag in ControllerAuraTags) p.ControllerAuraEffects[tag] = 1;
            p[GameTag.CHOOSE_BOTH] = 2;
        }
        var source = Add(g.Player1, "TIME_001");
        Check(g.Process(PlayCardTask.Any(g.Player1, source)), "EffectiveControllerReset alternate payment accepted");
        var expected = PinnedPoolFixture.LoadSnapshot(g.Rewind.Baseline);
        Answer(g, true);
        var reset = g.PowerHistory.Last.OfType<PowerHistoryResetGame>().Single();
        foreach (var player in reset.CreateGame.Players)
        {
            var p = expected.ControllerById(player.PowerEntity.Id);
            Check(p[GameTag.CHOOSE_BOTH] == 3, "EffectiveControllerReset raw plus active aura fixture");
            foreach (var tag in ControllerAuraTags)
                Check(player.PowerEntity.Tags.GetValueOrDefault(tag) == p[tag],
                    "EffectiveControllerReset " + tag + ": effective aura must survive reset");
            Check(player.PowerEntity.Tags[(GameTag)291] == p.CurrentSpellPower,
                "EffectiveControllerReset effective spell damage");
        }
        Console.WriteLine("PASS Rewind EffectiveControllerReset: all 13 controller aura tags, raw+aura composition, effective spell damage, both controllers");
    }

    private static void DiagnosticClonePolicy()
    {
        foreach (bool rewind in new[] { false, true })
        foreach (bool cloneHistory in new[] { true, false })
        {
            var source = RuntimeFixture(logging: true);
            Summon(source.Player1, "OG_044");
            // Leave only Clocksworth's slot: catalog growth must not summon Morchie's KeepBoth aura.
            for (int i = 0; i < 5; i++) Summon(source.Player1, "CS2_182");
            Play(source, source.Player1, "TIME_038");
            var clone = source.CloneExact(history: cloneHistory);
            var games = new[] { source, PinnedPoolFixture.LoadSnapshot(source.SaveSnapshot()), clone,
                PinnedPoolFixture.LoadSnapshot(clone.SaveSnapshot()) };
            foreach (var game in games)
            {
                bool logging = game.Logging;
                var logs = game.Logs;
                int count = logs?.Count ?? 0;
                Answer(game, rewind);
                Check(game.Logging == logging && ReferenceEquals(logs, game.Logs),
                    "DiagnosticClonePolicy receiving logging policy and queue retained");
                if (logging) Check(game.Logs.Count >= count, "DiagnosticClonePolicy diagnostic audit retained");
                else Check(game.Logs == null, "DiagnosticClonePolicy clone remains log-free");
                CheckLoggingFlags(game);
                var reload = PinnedPoolFixture.LoadSnapshot(game.SaveSnapshot());
                Check(reload.Logging == logging, "DiagnosticClonePolicy post-answer reload");
                if (rewind)
                {
                    Answer(game, true); Answer(reload, true);
                    Check(game.Logging == logging && reload.Logging == logging, "DiagnosticClonePolicy retry logging policy");
                    Answer(game, false); Answer(reload, false);
                }
                End(game); End(reload);
                Check(Canonical.State(game) == Canonical.State(reload), "DiagnosticClonePolicy reloaded future");
            }
            Check(games.Select(Canonical.State).Distinct().Count() == 1, "DiagnosticClonePolicy source/clone/load gameplay parity");
        }
        var quiet = RuntimeFixture(seed: 20); Play(quiet, quiet.Player1, "TIME_038");
#pragma warning disable CS0618 // The legacy API also permits enabling diagnostics on an existing offer.
        var verbose = quiet.Clone(logging: true, resetRandomSeed: false, history: true);
#pragma warning restore CS0618
        var queue = verbose.Logs;
        Answer(verbose, true);
        Check(verbose.Logging && ReferenceEquals(queue, verbose.Logs) && queue.Count > 0,
            "DiagnosticClonePolicy logging receiver with a log-free baseline");
        CheckLoggingFlags(verbose);
        Console.WriteLine("PASS Rewind DiagnosticClonePolicy: logging source/exact clone, emitted/suppressed history, Keep/Rewind/retry, save/load, receiver logging flags+queue");
    }

    private static IEnumerable<FieldInfo> InstanceFields(Type type)
    {
        for (; type != null && type != typeof(object); type = type.BaseType)
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                yield return field;
    }

    private static void CheckLoggingFlags(Game g)
    {
        foreach (object entity in new object[] { g, g.Player1, g.Player2 }.Concat(g.IdEntityDic.Values.Cast<object>()))
        foreach (var field in InstanceFields(entity.GetType()).Where(f => f.Name == "_logging"))
            Check((bool)field.GetValue(entity) == g.Logging, "DiagnosticClonePolicy cached logging flag: " + entity.GetType().Name);
    }

    private static void RuntimeServiceBindings(Game g)
    {
        var buses = new HashSet<object>(ReferenceEqualityComparer.Instance) { g.TriggerManager.Events };
        foreach (var character in g.IdEntityDic.Values.OfType<Character>()) buses.Add(character.Events);
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        void Visit(object value, string path)
        {
            if (value == null || value is string || value.GetType().IsPrimitive || value.GetType().IsEnum || !seen.Add(value)) return;
            if (value is Game) Check(ReferenceEquals(value, g), "RuntimeServiceBindings game at " + path);
            if (value is PowerHistory) Check(ReferenceEquals(value, g.PowerHistory), "RuntimeServiceBindings history at " + path);
            if (value is Util.DeepCloneableRandom) Check(ReferenceEquals(value, g.Random), "RuntimeServiceBindings RNG at " + path);
            if (value is TaskQueue) Check(ReferenceEquals(value, g.TaskQueue), "RuntimeServiceBindings task queue at " + path);
            if (value is ResolutionScheduler) Check(ReferenceEquals(value, g.Resolution), "RuntimeServiceBindings scheduler at " + path);
            if (value is GameEventBus) Check(buses.Contains(value), "RuntimeServiceBindings bus at " + path);
            if (value is Queue<LogEntry>) Check(ReferenceEquals(value, g.Logs), "RuntimeServiceBindings logs at " + path);
            if (value is Card) return; // Catalogue callbacks are immutable registered definitions.
            if (value is Delegate callback)
            {
                foreach (var item in callback.GetInvocationList()) Visit(item.Target, path + ".Target");
            }
            else if (value.GetType().Assembly == typeof(Game).Assembly || value.GetType().IsValueType)
                foreach (var field in InstanceFields(value.GetType())) Visit(field.GetValue(value), path + "." + field.Name);
            else if (value is IDictionary dictionary)
                foreach (DictionaryEntry item in dictionary) { Visit(item.Key, path + ".Key"); Visit(item.Value, path + ".Value"); }
            else if (value is IEnumerable sequence)
                foreach (var item in sequence) Visit(item, path + "[]");
        }
        Visit(g, "Game");
    }

    private static string FutureActionKey(PlayerTask task) =>
        $"{task.PlayerTaskType}:{task.Controller.Id}:{task.Source?.Id}:{task.ActionTarget?.Id}:" +
        $"{(task is PlayCardTask play ? play.ZonePosition : 0)}:{task.ChooseOne}:" +
        (task is ChooseTask choice ? string.Join(",", choice.Choices) : "");

    private static void RuntimeRandomFuture()
    {
        int actions = 0;
        foreach (int seed in new[] { 1, 19, 31, 71 })
        {
            var g = RuntimeFixture(seed);
            foreach (var p in new[] { g.Player1, g.Player2 })
            {
                foreach (string id in new[] { "LOE_077", "OG_044", "CS2_142", "EX1_350", "EX1_162", "NEW1_019" }) Summon(p, id);
                foreach (string id in new[] { "EX1_154", "CS2_029", "CS2_008", "LOE_077", "OG_044", "CS2_142", "EX1_015" }) Add(p, id);
            }
            Play(g, g.Player1, "TIME_001"); Answer(g, true);
            var games = new[] { g, g.CloneExact(history: true), PinnedPoolFixture.LoadSnapshot(g.SaveSnapshot()) };
            // Exact clones start new output streams; compare only entries emitted by this future.
            foreach (var game in games) { game.PowerHistory.Full.Clear(); game.PowerHistory.Last.Clear(); }
            var policy = new PolicyRandom((ulong)seed);
            for (int step = 0; step < 20; step++)
            {
                Check(g.State == State.RUNNING, "RuntimeRandomFuture must exercise all 20 actions");
                var options = games.Select(game => game.CurrentPlayer.Options().Where(t => t.PlayerTaskType != PlayerTaskType.CONCEDE)
                    .OrderBy(FutureActionKey, StringComparer.Ordinal).ToList()).ToArray();
                Check(options.All(list => list.Select(FutureActionKey).SequenceEqual(options[0].Select(FutureActionKey))),
                    $"RuntimeRandomFuture legal actions seed={seed}, step={step}");
                int pick = policy.Index(options[0].Count);
                for (int i = 0; i < games.Length; i++) Check(games[i].Process(options[i][pick]), "RuntimeRandomFuture legal action accepted");
                Check(games.Select(Canonical.State).Distinct().Count() == 1 && games.Select(game => game.Random.CaptureState()).Distinct().Count() == 1 &&
                    games.Select(game => game.HistoryFacts.CanonicalText()).Distinct().Count() == 1,
                    $"RuntimeRandomFuture state/RNG/facts seed={seed}, step={step}");
                Check(games.Select(game => Canonical.Value(game.PowerHistory.Last)).Distinct().Count() == 1 &&
                    games.Select(game => Canonical.Value(game.PowerHistory.Full)).Distinct().Count() == 1,
                    $"RuntimeRandomFuture PowerHistory entry stream seed={seed}, step={step}, action={FutureActionKey(options[0][pick])}");
                actions++;
            }
            foreach (var game in games) RuntimeServiceBindings(game);
        }
        Console.WriteLine($"PASS Rewind RuntimeRandomFuture: seeds=4; actions={actions}; 20-action source/load/CloneExact state/RNG/facts/PowerHistory streams; Brann/Fandral/spell-damage/controller/adjacent auras and triggers; graph service identities");
    }

    internal static void RunRoundTwo(string only = null)
    {
        foreach (var (name, proof) in new (string, Action)[] { ("history", AuraHistoryRebinding),
            ("projection", EffectiveControllerReset), ("logging", DiagnosticClonePolicy), ("future", RuntimeRandomFuture) })
            if (only == null || only == name) proof();
    }
}
