using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;

namespace HsSim;

public sealed class GoldenSuite
{
    public int Schema { get; set; } =
#if SOURCE_ENGINE
        3;
#else
        2;
#endif
    public string Suite { get; set; }
    public string Policy { get; set; }
    public string EngineSha { get; set; }
    public List<GameRecord> Games { get; set; } = new();
}

public static class Program
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        try
        {
            if (args.Length == 0) throw new ArgumentException("usage: hs build|check|sim|goldens|fuzz|replay (see sim/README.md)");
            if (args[0] == "goldens")
            {
                if (args.Length == 2 && args[1] == "rerecord") throw new UsageException(RerecordUsage);
                if (args.Length < 3) throw new ArgumentException("goldens record|check|rerecord <file>");
                if (args[1] == "rerecord") return Rerecord(args[2], args[3..]);
                if (args[1] == "record")
                {
                    var options = Options(args[3..], "suite", "seeds", "policy", "pool-snapshot");
                    string suite = Get(options, "suite", "classic"), policy = Policy(options);
                    var watch = Stopwatch.StartNew();
                    var data = new GoldenSuite { Suite = suite, Policy = policy,
                        EngineSha = EngineSha() };
#if SOURCE_ENGINE
                    var pools = PoolSnapshots.Save(Get(options, "pool-snapshot", null));
#endif
                    foreach (var record in Cases(suite, Seeds(Get(options, "seeds", "0..0")), policy))
                    {
                        Simulation.Finish(record, Simulation.Run(record
#if SOURCE_ENGINE
                            , pools: pools
#endif
                            ));
                        data.Games.Add(record);
                    }
                    Save(args[2], data);
                    Console.Error.WriteLine($"recorded {data.Games.Count} games: {args[2]} ({watch.Elapsed.TotalSeconds:F3}s)");
                    return 0;
                }
                if (args[1] == "check")
                {
                    Options(args[3..]);
                    GoldenSuite data = Load<GoldenSuite>(args[2]);
                    ValidateSuite(data);
                    var watch = Stopwatch.StartNew();
                    int matched = 0;
                    foreach (GameRecord record in data.Games) if (Check(record, false)) matched++;
                    Console.Error.WriteLine($"goldens: {matched}/{data.Games.Count} match; mismatches={data.Games.Count - matched}; wall={watch.Elapsed.TotalSeconds:F3}s");
                    return matched == data.Games.Count ? 0 : 1;
                }
                throw new ArgumentException("goldens record|check|rerecord <file>");
            }
            if (args[0] == "sim")
            {
                var options = Options(args[1..], "suite", "seeds", "policy");
#if SOURCE_ENGINE
                PoolSnapshots.Save();
#endif
                foreach (var record in Cases(Get(options, "suite", "classic"), Seeds(Get(options, "seeds", "0..0")), Policy(options)))
                {
                    Simulation.Finish(record, Simulation.Run(record));
                    Console.WriteLine(JsonSerializer.Serialize(record, Json));
                }
                return 0;
            }
            if (args[0] == "replay")
            {
                if (args.Length < 2) throw new ArgumentException("replay <file.json> [--dump]");
                var options = Options(args[2..], "dump");
                return Check(Load<GameRecord>(args[1]), options.ContainsKey("dump")) ? 0 : 1;
            }
            if (args[0] == "fuzz")
            {
                var options = Options(args[1..], "games", "seed", "clone-probes");
                int games = Positive(Get(options, "games", "300"));
                int seed = int.Parse(Get(options, "seed", "1"));
                return Fuzz(games, seed, options.ContainsKey("clone-probes"));
            }
            throw new ArgumentException("unknown command: " + args[0]);
        }
        catch (UsageException error)
        {
            Console.Error.WriteLine("usage: " + error.Message);
            return 2;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"{error.GetType().Name}: {error.Message}");
            return 1;
        }
    }

    private static string EngineSha() => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(Game).Assembly.Location)));

    private sealed class UsageException(string message) : ArgumentException(message);

    private const string RerecordUsage = "goldens rerecord <file> [--only <game-id>...] [--dry-run] [--allow-state-change] [--allow-behaviour-change]";

    private static void ValidateSuite(GoldenSuite data)
    {
        if (data.Schema is not (2 or 3) || data.Games == null || data.Games.Count == 0) throw new ArgumentException("empty or unsupported golden suite (requires schema 2 or 3)");
        foreach (var record in data.Games)
        {
            if (data.Schema == 2 && record.ReplaySchema == null) record.ReplaySchema = 2;
            if (record.ReplaySchema != data.Schema) throw new ArgumentException("golden suite/replay schema mismatch");
        }
    }

    // Replays every game (or the --only ones) and re-records just the mismatching ones, each with the pool snapshot
    // pinned in its own record, so one intended engine change cannot drag the rest of the file along with it.
    private static int Rerecord(string path, string[] rest)
    {
        var only = new List<string>();
        bool dryRun = false, allowBehaviour = false, allowState = false;
        for (int i = 0; i < rest.Length; i++)
        {
            if (rest[i] == "--dry-run") dryRun = true;
            else if (rest[i] == "--allow-behaviour-change") allowBehaviour = true;
            else if (rest[i] == "--allow-state-change") allowState = true;
            else if (rest[i] == "--only")
            {
                int first = i + 1;
                while (i + 1 < rest.Length && !rest[i + 1].StartsWith("--", StringComparison.Ordinal)) only.Add(rest[++i]);
                if (i + 1 == first) throw new UsageException(RerecordUsage);
            }
            else throw new UsageException(RerecordUsage);
        }
#if SOURCE_ENGINE
        string text = File.ReadAllText(path);
        GoldenSuite data = Deserialize<GoldenSuite>(text);
        ValidateSuite(data);
        string[] unknown = only.Where(id => data.Games.All(record => record.Id != id)).ToArray();
        if (unknown.Length > 0) throw new UsageException("no such game in the file: " + string.Join(", ", unknown));
        var watch = Stopwatch.StartNew();
        var replaced = new Dictionary<int, GameRecord>();
        var scope = new List<int>();
        var behavioural = new List<string>();
        var stateOnly = new List<string>();
        int failed = 0;
        for (int i = 0; i < data.Games.Count; i++)
        {
            GameRecord old = data.Games[i];
            if (only.Count > 0 && !only.Contains(old.Id)) continue;
            scope.Add(i);
            if (Check(old, false)) continue;
            try
            {
                GameRecord fresh = Recreate(old, data.Schema);
                replaced[i] = fresh;
                string line = Classify(old, fresh, out bool behaviour, out bool stateMoved);
                if (behaviour) behavioural.Add(old.Id);
                else if (stateMoved) stateOnly.Add(old.Id);
                Console.WriteLine($"changed {old.Id}: {line}");
            }
            catch (Exception error)
            {
                failed++;
                Console.Error.WriteLine($"cannot re-record {old.Id}: {error.Message}");
            }
        }
        if (failed > 0)
        {
            Console.WriteLine($"{replaced.Count} re-recorded, {failed} could not be; nothing written");
            return 1;
        }
        if (behavioural.Count > 0)
        {
            // Same actions and legal moves mean only the recorded hashes moved. Anything else is a different game: a rules,
            // deck or card change. That needs a reviewer to look at each listed game, so it is opt-in.
            if (!allowBehaviour)
            {
                Console.Error.WriteLine("refusing behaviour changes without --allow-behaviour-change: " + string.Join(", ", behavioural));
                Console.WriteLine($"{replaced.Count} could be re-recorded ({behavioural.Count} behaviour); nothing written");
                return 1;
            }
            foreach (string id in behavioural)
                Console.Error.WriteLine($"WARNING {id}: BEHAVIOUR change re-recorded on request; review this game before committing");
        }
        if (stateOnly.Count > 0)
        {
            // The legal-move hashes cover the options offered, not the board numbers, so a rules change that alters stats or
            // tags without changing any offered option moves only stateSha. That cannot be told from a harmless change by
            // the hashes alone, so it needs a reviewer too. --allow-behaviour-change covers it (a superset).
            if (!allowState && !allowBehaviour)
            {
                Console.Error.WriteLine("refusing STATE changes without --allow-state-change: " + string.Join(", ", stateOnly));
                Console.WriteLine($"{replaced.Count} could be re-recorded ({stateOnly.Count} state); nothing written");
                return 1;
            }
            foreach (string id in stateOnly)
                Console.Error.WriteLine($"WARNING {id}: STATE change re-recorded on request; replay it and compare the final board before committing");
        }
        if (replaced.Count > 0)
        {
            // Rewrite only if the file round-trips, so every game we did not touch stays byte-identical.
            GoldenSuite output = Deserialize<GoldenSuite>(text);
            if ((JsonSerializer.Serialize(output, Json) + "\n").ReplaceLineEndings("\n") != text.ReplaceLineEndings("\n"))
                throw new InvalidDataException("golden file is not in the form `goldens record` writes; refusing to rewrite it");
            foreach (var (index, fresh) in replaced) output.Games[index] = fresh;
            output.EngineSha = EngineSha();
            // Re-check what would be written, loaded back from its own text, before touching the file. Only the re-recorded
            // games need it: the others matched above, and the round-trip check shows their records did not change.
            string result = JsonSerializer.Serialize(output, Json) + "\n";
            GoldenSuite reloaded = Deserialize<GoldenSuite>(result);
            ValidateSuite(reloaded);
            string[] still = replaced.Keys.Where(index => !Check(reloaded.Games[index], false)).Select(index => reloaded.Games[index].Id).ToArray();
            if (still.Length > 0)
            {
                Console.Error.WriteLine("still mismatching after re-recording: " + string.Join(", ", still));
                Console.WriteLine($"{replaced.Count} re-recorded, {still.Length} still mismatch; nothing written");
                return 1;
            }
            if (!dryRun) Save(path, output);
        }
        string all = only.Count > 0 ? $"all {scope.Count} in scope" : $"all {scope.Count}";
        Console.WriteLine($"{replaced.Count} re-recorded ({replaced.Count - behavioural.Count - stateOnly.Count} shape only, {stateOnly.Count} state, {behavioural.Count} behaviour), {scope.Count - replaced.Count} unchanged, {all} match"
            + (dryRun ? "; dry run, nothing written" : "") + $" ({watch.Elapsed.TotalSeconds:F3}s)");
        return 0;
#else
        throw new ArgumentException("goldens rerecord needs the source engine build (-p:EngineSource=true)");
#endif
    }

#if SOURCE_ENGINE
    // One line saying how a re-recorded game differs. SHAPE: same actions and legal moves (so same turns and winner), only
    // the history hash moved. STATE: same, but the final state hash moved: the board numbers or tags changed without
    // changing any offered option, which the other hashes cannot see. BEHAVIOUR: anything else, including a changed rules/pool version field.
    private static string Classify(GameRecord old, GameRecord fresh, out bool behaviour, out bool stateMoved)
    {
        static int Differing<T>(List<T> a, List<T> b) =>
            Enumerable.Range(0, Math.Max(a.Count, b.Count)).Count(i => i >= a.Count || i >= b.Count || !EqualityComparer<T>.Default.Equals(a[i], b[i]));
        static int FirstDiff<T>(List<T> a, List<T> b) =>
            Enumerable.Range(0, Math.Max(a.Count, b.Count)).First(i => i >= a.Count || i >= b.Count || !EqualityComparer<T>.Default.Equals(a[i], b[i]));
        var others = new List<string>();
        if (old.PoolPolicyVersion != fresh.PoolPolicyVersion) others.Add($"poolPolicyVersion {old.PoolPolicyVersion ?? "none"}->{fresh.PoolPolicyVersion ?? "none"}");
        if (old.DarkGiftPolicyVersion != fresh.DarkGiftPolicyVersion) others.Add($"darkGiftPolicyVersion {old.DarkGiftPolicyVersion ?? "none"}->{fresh.DarkGiftPolicyVersion ?? "none"}");
        if (old.Failure != null) others.Add("recorded failure dropped");
        int actions = Differing(old.Actions, fresh.Actions), legal = Differing(old.LegalShas, fresh.LegalShas);
        behaviour = actions > 0 || legal > 0 || old.Turns != fresh.Turns || old.Winner != fresh.Winner || others.Count > 0;
        stateMoved = !behaviour && old.StateSha != fresh.StateSha;
        if (stateMoved)
            return $"STATE final state moved ({(old.HistorySha != fresh.HistorySha ? "history and state" : "state only")}); actions, turns and winner unchanged; replay it and compare the final board";
        if (!behaviour)
        {
            var moved = new List<string>();
            if (old.HistorySha != fresh.HistorySha) moved.Add("history");
            return $"shape only ({(moved.Count > 0 ? string.Join(", ", moved) : "no hash")} moved), 0 actions changed";
        }
        string text = $"BEHAVIOUR winner {old.Winner}->{fresh.Winner}, turns {old.Turns}->{fresh.Turns}, actions {old.Actions.Count}->{fresh.Actions.Count} ({actions} changed"
            + (actions > 0 ? $", first at step {FirstDiff(old.Actions, fresh.Actions)}" : "") + ")";
        if (legal > 0) text += $", legal moves differ from step {FirstDiff(old.LegalShas, fresh.LegalShas)}";
        return others.Count > 0 ? text + "; " + string.Join(", ", others) : text;
    }

    // The same case again (suite, decks, seed, policy), made the way `goldens record` makes it. Schema 3 runs on the pool
    // snapshot the old record names. Schema 2 records no pool, so it runs unpinned, under today's pool and Dark Gift rules,
    // and is stored without those fields. Replaying it later uses the legacy rules, so the whole-file re-check after
    // re-recording is what guards a schema 2 game: if the legacy replay does not reproduce it, nothing is written.
    private static GameRecord Recreate(GameRecord old, int schema)
    {
        GameRecord fresh = Cases(old.Suite, (old.Seed, old.Seed), old.Policy)
            .FirstOrDefault(c => c.Player == old.Player && c.Opponent == old.Opponent)
            ?? throw new InvalidOperationException($"case {old.Player} vs {old.Opponent} can no longer be built in suite {old.Suite}");
        if (fresh.Id != old.Id || fresh.Suite != old.Suite || fresh.Player != old.Player || fresh.Opponent != old.Opponent
            || fresh.Seed != old.Seed || fresh.Policy != old.Policy)
            throw new InvalidOperationException($"rebuilt case {fresh.Id} differs from the record {old.Id}");
        fresh.ClonePoints = old.ClonePoints;
        OfflinePoolManifest pools = schema == 3 ? PoolSnapshots.ForReplay(old.PoolManifestHash) : null;
        Simulation.Finish(fresh, Simulation.Run(fresh, pools: pools));
        if (schema == 3)
        {
            if (fresh.PoolManifestHash != old.PoolManifestHash) throw new InvalidOperationException("pool snapshot was not used");
        }
        else
        {
            // A schema-2 file stores no replay schema, pool or Dark Gift fields.
            fresh.ReplaySchema = null;
            fresh.PoolPolicyVersion = null;
            fresh.PoolManifestHash = null;
            fresh.DarkGiftPolicyVersion = null;
        }
        Validate(fresh);
        return fresh;
    }
#endif

    private static string Policy(Dictionary<string, string> options)
    {
        string policy = Get(options, "policy", "random");
        if (policy is not ("random" or "first")) throw new ArgumentException("policy must be random or first");
        return policy;
    }

    private static Dictionary<string, string> Options(string[] args, params string[] allowed)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i++)
        {
            string key = args[i].StartsWith("--", StringComparison.Ordinal) ? args[i][2..] : "";
            if (!allowed.Contains(key) || values.ContainsKey(key)) throw new ArgumentException("unknown or duplicate option: " + args[i]);
            if (key is "dump" or "clone-probes") values.Add(key, "true");
            else
            {
                if (++i >= args.Length) throw new ArgumentException("missing option value: " + key);
                values.Add(key, args[i]);
            }
        }
        return values;
    }

    private static string Get(Dictionary<string, string> options, string key, string fallback) => options.GetValueOrDefault(key, fallback);
    private static int Positive(string value) => int.TryParse(value, out int number) && number > 0 ? number : throw new ArgumentException("positive count required");
    private static (int Start, int End) Seeds(string range)
    {
        string[] parts = range.Split("..", StringSplitOptions.None);
        if (parts.Length != 2 || !int.TryParse(parts[0], out int start) || !int.TryParse(parts[1], out int end) || start > end)
            throw new ArgumentException("seeds require inclusive A..B with A <= B (int32)");
        return (start, end);
    }

    private static IEnumerable<GameRecord> Cases(string suite, (int Start, int End) seeds, string policy)
    {
        string[] ids = Simulation.DeckIds(suite);
        foreach (string first in ids)
        foreach (string second in ids)
        {
            string[] missing = Simulation.Missing(Simulation.Deck(first)).Concat(Simulation.Missing(Simulation.Deck(second))).Distinct().ToArray();
            if (missing.Length > 0)
            {
                if (suite != "modern") throw new ArgumentException("missing Classic cards: " + string.Join(",", missing));
                Console.Error.WriteLine($"skip {first}/{second}: missing cards {string.Join(",", missing)}");
                continue;
            }
            for (long seed = seeds.Start; seed <= seeds.End; seed++)
                yield return new GameRecord { Id = $"{suite}-{first}-vs-{second}-s{seed}-{policy}", Suite = suite,
                    Player = first, Opponent = second, Seed = (int)seed, Policy = policy };
        }
    }

    private static bool Check(GameRecord expected, bool dump)
    {
        Validate(expected);
        Game currentGame = null;
        try
        {
            Game game = Simulation.Run(expected, replay: true, started: game => currentGame = game);
            var current = new GameRecord();
            Simulation.Finish(current, game);
            bool matches = current.HistorySha == expected.HistorySha && current.StateSha == expected.StateSha
                && current.Turns == expected.Turns && current.Winner == expected.Winner;
            if (dump || !matches) Dump(expected.Id, game);
            if (!matches)
                Console.Error.WriteLine($"mismatch {expected.Id}: recorded history={expected.HistorySha} state={expected.StateSha} turns={expected.Turns} winner={expected.Winner}; current history={current.HistorySha} state={current.StateSha} turns={current.Turns} winner={current.Winner}");
            if (expected.Failure != null)
            {
                Console.Error.WriteLine($"{expected.Id}: recorded failure did not reproduce: {expected.Failure}");
                return false;
            }
            return matches;
        }
        catch (Exception error)
        {
            if (currentGame != null) Dump(expected.Id, currentGame);
            Diagnostics(expected.Id, error);
            Console.Error.WriteLine($"mismatch {expected.Id}: {error.Message}; recorded history={expected.HistorySha} state={expected.StateSha}");
            return false;
        }
    }

    private static int Fuzz(int count, int seed, bool cloneProbes)
    {
#if SOURCE_ENGINE
        PoolSnapshots.Save();
#endif
        var watch = Stopwatch.StartNew();
        var random = new PolicyRandom(unchecked((ulong)(long)seed));
        string[] ids = Simulation.FuzzDeckIds()
            .Where(id => Simulation.Missing(Simulation.Deck(id)).Length == 0).ToArray();
        if (ids.Length == 0) throw new ArgumentException("no supported decks");
        var failures = new Dictionary<string, int>(StringComparer.Ordinal);
        int probes = 0, completed = 0, deterministic = 0;
        var reprintsPlayed = new HashSet<string>(StringComparer.Ordinal);
        var reprintsTraded = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < count; i++)
        {
            string first = ids[random.Index(ids.Length)], second = ids[random.Index(ids.Length)];
            int gameSeed = unchecked((int)random.Next());
            var record = new GameRecord { Id = $"fuzz-{seed}-{i:D4}", Suite = "mixed", Player = first,
                Opponent = second, Seed = gameSeed, Policy = "random" };
            try
            {
                Simulation.Finish(record, Simulation.Run(record, processed: task => {
                    if (task.PlayerTaskType == PlayerTaskType.PLAY_CARD && Simulation.IsCoreReprint(task.Source?.Card))
                        reprintsPlayed.Add(task.Source.Card.Id);
#if SOURCE_ENGINE
                    if (task.PlayerTaskType == PlayerTaskType.TRADE_CARD && Simulation.IsCoreReprint(task.Source?.Card))
                        reprintsTraded.Add(task.Source.Card.Id);
#endif
                }));
                completed++;
                Game repeat = Simulation.Run(record, replay: true);
#if SOURCE_ENGINE
                // A normal game must keep its achievement evidence; fail-open recording must not hide recorder regressions.
                if (!repeat.HistoryFacts.EvidenceValid)
                    throw new InvariantException("history.evidence-invalid", repeat.HistoryFacts.EvidenceFailure);
#endif
                if (Canonical.Sha(Canonical.History(repeat)) != record.HistorySha)
                    throw new InvariantException("determinism.history", "repeat history differs");
                if (Canonical.Sha(Canonical.State(repeat)) != record.StateSha)
                    throw new InvariantException("determinism.state", "repeat state differs");
                deterministic++;
                if (cloneProbes)
                {
                    // Sample three distinct points from the actual game, including short games; separate probe PRNG.
                    var points = new PolicyRandom(unchecked((ulong)(long)gameSeed) ^ 0x434C4F4E45UL);
                    var selected = new HashSet<int>();
                    int eligible = record.Actions.Count >= 22 ? record.Actions.Count - 19 : record.Actions.Count;
                    while (selected.Count < Math.Min(3, eligible)) selected.Add(points.Index(eligible));
                    record.ClonePoints = selected.OrderBy(point => point).ToList();
                    Simulation.Run(record, replay: true);
                    probes += selected.Count;
                }
            }
            catch (Exception error)
            {
                string invariant = error is InvariantException check ? check.Invariant : "engine.exception";
                failures[invariant] = failures.GetValueOrDefault(invariant) + 1;
                record.Failure = error.ToString();
                string path = Path.Combine("sim", "failures", record.Id + ".json");
                Save(path, record);
                Diagnostics(record.Id, error);
                Console.Error.WriteLine($"FAIL {record.Id} {first}/{second} seed={gameSeed} invariant={invariant}: {error.Message}; replay={path}");
            }
            if ((i + 1) % 25 == 0) Console.Error.WriteLine($"fuzz progress {i + 1}/{count}; failed={failures.Values.Sum()}");
        }
        Console.Error.WriteLine($"fuzz: games={count}; failures={failures.Values.Sum()}; completed={completed}; deterministic={deterministic}; cloneProbes={cloneProbes.ToString().ToLowerInvariant()}; cloneProbesPassed={probes}; reprintsPlayed={reprintsPlayed.Count}; reprintsTraded={JsonSerializer.Serialize(reprintsTraded.Order(StringComparer.Ordinal))}; invariants={JsonSerializer.Serialize(failures)}; wall={watch.Elapsed.TotalSeconds:F3}s");
        return failures.Count == 0 ? 0 : 1;
    }

    private static void Diagnostics(string id, Exception error)
    {
        foreach (string key in new[] { "originalState", "cloneState" })
            if (error.Data[key] is string state)
            {
                Directory.CreateDirectory("sim/out");
                File.WriteAllText(Path.Combine("sim/out", id + "." + key), state);
            }
    }

    private static void Validate(GameRecord record)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.Id) || record.Id.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-' && character != '_'))
            throw new ArgumentException("game id must contain only ASCII letters, digits, '-' or '_'");
        if (record.Player == null || record.Opponent == null || record.Actions == null) throw new ArgumentException("missing game fields");
        if (record.ClonePoints?.Any(point => point < 0 || point >= record.Actions.Count) == true) throw new ArgumentException("clone point outside actions");
    }

    private static void Dump(string id, Game game)
    {
        string path = Path.Combine("sim", "out", id + ".trace");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, Canonical.Trace(game));
        Console.Error.WriteLine("trace: " + path);
    }

    private static T Load<T>(string path) => Deserialize<T>(File.ReadAllText(path));
    private static T Deserialize<T>(string text) => JsonSerializer.Deserialize<T>(text, Json)
        ?? throw new ArgumentException("null JSON");
    private static void Save<T>(string path, T value)
    {
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        string temporary = full + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, Json) + "\n");
        File.Move(temporary, full, overwrite: true);
    }
}
