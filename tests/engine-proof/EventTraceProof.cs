using System.Reflection;
using HsSim;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class EventTraceProof
{
    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    // Stable callback identity includes its owning entity, not runtime object hashes.
    private sealed class Recorder
    {
        private readonly Dictionary<object, int> targets = new(ReferenceEqualityComparer.Instance);
        internal readonly List<string> Lines = new();

        internal void Record(string channel, TriggerManager.TriggerHandler handler, int order)
        {
            object target = handler.Target;
            int identity = 0;
            if (target != null && !targets.TryGetValue(target, out identity))
                targets.Add(target, identity = targets.Count + 1);
            var owners = new List<string>();
            for (System.Type type = target?.GetType(); type != null; type = type.BaseType)
                foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public |
                             BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(f => f.Name))
                {
                    object value = field.GetValue(target);
                    if (value is IEntity entity) owners.Add(field.Name + "=" + entity.Id);
                    else if (field.Name is "_sourceId" or "_ownerId") owners.Add(field.Name + "=" + value);
                }
            Lines.Add($"{channel}|{order}|{identity}:{handler.Method.DeclaringType.FullName}.{handler.Method.Name}|{string.Join(',', owners)}");
        }
    }

    private static void Attach(TriggerManager manager, Recorder recorder)
    {
        manager.Events.Trace = (value, handler, order) => recorder.Record(value.Channel, handler, order);
    }

    private static Game Create(long seed)
    {
        string[] cards = { "EX1_007", "NEW1_019", "NEW1_020", "EX1_044", "CS2_122",
            "CS2_024", "EX1_277", "EX1_096", "CS2_029", "CS2_182" };
        return new Game(new GameConfig {
            RandomSeed = seed, StartPlayer = -1, Shuffle = true, SkipMulligan = true,
            Logging = false, History = true, FormatType = FormatType.FT_WILD,
            Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.MAGE,
            Player1Deck = Enumerable.Range(0, 30).Select(i => Cards.FromId(cards[i % cards.Length])).ToList(),
            Player2Deck = Enumerable.Range(0, 30).Select(i => Cards.FromId(cards[(i + 3) % cards.Length])).ToList()
        });
    }

    private static void Advance(Game game, PolicyRandom policy)
    {
        var legal = game.CurrentPlayer.Options().OrderBy(t => (int)t.PlayerTaskType)
            .ThenBy(t => t.Source?.Id ?? 0).ThenBy(t => t.Target?.Id ?? 0)
            .ThenBy(t => t is PlayCardTask play ? play.ZonePosition : 0).ThenBy(t => t.ChooseOne).ToList();
        Check(legal.Count > 0 && game.Process(legal[policy.Index(legal.Count)]), "trace game accepts legal action");
    }

    private static string Run(long seed, bool trace)
    {
        Game game = Create(seed);
        var recorder = new Recorder();
        if (trace) Attach(game.TriggerManager, recorder);
        game.StartGame();
        var policy = new PolicyRandom((ulong)seed);
        Game clone = null;
        var cloneRecorder = new Recorder();
        int steps = 0;
        while (game.State != State.COMPLETE && steps++ < 2000)
        {
            Advance(game, policy);
            if (steps == 30)
            {
                clone = game.CloneExact(history: true);
                if (trace) Attach(clone.TriggerManager, cloneRecorder);
            }
        }
        Check(game.State == State.COMPLETE && clone != null, "trace game terminates and exercises clone");
        policy = new PolicyRandom((ulong)seed ^ 1234);
        steps = 0;
        while (clone.State != State.COMPLETE && steps++ < 2000) Advance(clone, policy);
        Check(clone.State == State.COMPLETE, "trace clone terminates");
        string history = $"history={Canonical.Sha(Canonical.Trace(game))}\nclone={Canonical.Sha(Canonical.Trace(clone))}\n";
        return trace ? string.Join('\n', recorder.Lines) + "\n--- CLONE ---\n" +
            string.Join('\n', cloneRecorder.Lines) + "\n" + history : history;
    }

    // PR5 fixes stale clone tags/registry state. Retain the frozen invocation traces and live
    // history/state hash; CloneProof now checks the clone against the source's actual future.
    private static string LegacyContract(string trace) => trace[..trace.LastIndexOf("clone=", StringComparison.Ordinal)];

    internal static void Run()
    {
        long[] seeds = { 1, 19, 42, 123 };
        // On mismatch, leave every actual trace in the temp directory for diffing before any check fails.
        foreach (long seed in seeds)
        {
            string actual = Run(seed, true);
            if (LegacyContract(actual) != LegacyContract(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "event-traces", seed + ".txt"))))
                File.WriteAllText(Path.Combine(Path.GetTempPath(), $"event-trace-actual-{seed}.txt"), actual);
        }
        foreach (long seed in seeds)
        {
            string actual = Run(seed, true);
            string path = Path.Combine(AppContext.BaseDirectory, "event-traces", seed + ".txt");
            Check(LegacyContract(actual) == LegacyContract(File.ReadAllText(path)), $"legacy invocation trace/history differs for seed {seed}");
            Check(actual.EndsWith(Run(seed, false), StringComparison.Ordinal), "trace does not change history/state");
        }
        Console.WriteLine("PASS seeded legacy invocation traces, clone reconstruction, trace on/off history");
    }
}
