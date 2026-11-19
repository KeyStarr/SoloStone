using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HsSim;
using SabberStoneCore.Enums;

// Renewal of a pinned transport replay (tests/transport-invariants/replays) after an intended rules change.
//   EngineProof.dll --renew-replay PATH [--dump DIR] [--baseline DIR [--accept "LABEL;LABEL" [--accept-behaviour "SHA: reason"] --write]]
// It replays the recorded actions on the current engine and reports where the legal-move hash chain and the final state/RNG
// fingerprint drift. --dump writes each step's canonical state, chosen move and RNG state (run it on the last passing commit);
// --baseline compares this engine with such a dump and prints what drifted, by label. --write rewrites only `legalShas` and
// `engineFingerprint`; it needs a baseline and --accept naming exactly the printed labels, including legal moves and unlabelled state.
// It refuses when a recorded action is no longer legal, when the chosen move at a step changed, when any RNG state differs from the baseline, and, without
// --accept-behaviour "SHA: reason", when the turn count or the winner changes. Exit 1 reports drift without writing.
internal static partial class PinnedReplayEngineProof
{
    private static string Opt(string[] args, string name) { int at = Array.IndexOf(args, name); return at >= 0 && at + 1 < args.Length ? args[at + 1] : null; }

    internal static int Renew(string[] args)
    {
        string path = Path.GetFullPath(args[Array.IndexOf(args, "--renew-replay") + 1]);
        string dump = Opt(args, "--dump"), baseline = Opt(args, "--baseline"), accept = Opt(args, "--accept"), behaviour = Opt(args, "--accept-behaviour");
        bool write = args.Contains("--write");
        if (write && baseline == null) { Console.Error.WriteLine("--write needs --baseline DIR: see what drifted first"); return 1; }
        if (dump != null) Directory.CreateDirectory(dump);
        string text = File.ReadAllText(path);
        var replay = JsonSerializer.Deserialize<PinnedReplay>(text, Json);
        var record = replay.Game;
        var game = Simulation.Create(record, replay: true, playerDeck: replay.PlayerDeck, opponentDeck: replay.OpponentDeck);
        var legalNow = new List<string>();
        var drift = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        var problems = new List<string>();
        int step = 0, firstDrift = -1;
        while (game.State != State.COMPLETE)
        {
            if (game.Turn > 200 || step >= 20000) { problems.Add("game exceeded termination bounds at step " + step); break; }
            var legal = Simulation.Legal(game);
            string hash = Canonical.Sha(Canonical.Value(legal.Select(Simulation.TaskKey).ToArray()));
            if (step >= record.Actions.Count) { problems.Add($"step {step}: the game is not over but the recording has no action left"); break; }
            if (record.Actions[step] < 0 || record.Actions[step] >= legal.Count) { problems.Add($"step {step}: recorded action {record.Actions[step]} is no longer legal ({legal.Count} legal moves)"); break; }
            legalNow.Add(hash);
            string chosen = Simulation.TaskKey(legal[record.Actions[step]]);
            string rng = Canonical.Value(game.Random.CaptureState());
            string state = Canonical.State(game);
            string snapshot = "action:" + chosen + "\nrng:" + rng + "\n" + state;
            if (dump != null) File.WriteAllText(Path.Combine(dump, step.ToString("000") + ".txt"), snapshot);
            bool moved = step >= record.LegalShas.Count || hash != record.LegalShas[step];
            if (moved && firstDrift < 0) firstDrift = step;
            if (baseline != null)
            {
                string old = File.ReadAllText(Path.Combine(baseline, step.ToString("000") + ".txt"));
                string[] oldHead = old.Split('\n', 3), nowHead = snapshot.Split('\n', 3);
                if (oldHead[0] != nowHead[0]) problems.Add($"step {step}: the chosen move changed ({oldHead[0]} -> {nowHead[0]})");
                if (oldHead[1] != nowHead[1]) problems.Add($"step {step}: the RNG state differs");
                if (oldHead[2] != nowHead[2])
                    foreach (var change in StateDiff(oldHead[2], nowHead[2]))
                    {
                        if (!drift.TryGetValue(change.Key, out var seen)) drift[change.Key] = seen = new List<string>();
                        seen.Add("#" + step + " " + change.Value);
                    }
            }
            try { Simulation.Process(game, legal[record.Actions[step]]); }
            catch (Exception error) { problems.Add($"step {step}: action failed: {error.Message}"); break; }
            step++;
        }
        var current = new GameRecord(); Simulation.Finish(current, game);
        string fingerprint = Fingerprint(game);
        var behaviourChanges = new List<string>();
        if (problems.Count == 0 && step != record.Actions.Count) problems.Add($"the game ended after {step} of {record.Actions.Count} recorded actions");
        if (current.Turns != record.Turns) behaviourChanges.Add($"turns {record.Turns} -> {current.Turns}");
        if (current.Winner != record.Winner) behaviourChanges.Add($"winner {record.Winner} -> {current.Winner}");
        int changedHashes = 0;
        for (int i = 0; i < legalNow.Count; i++) if (i >= record.LegalShas.Count || legalNow[i] != record.LegalShas[i]) changedHashes++;
        if (fingerprint != replay.EngineFingerprint && drift.Count == 0)
            drift["unlabelled state"] = new List<string> { "final fingerprint differs without a labelled state change" };
        if (changedHashes != 0) drift["legal moves"] = new List<string> { changedHashes + " legal-move hashes differ" };
        Console.WriteLine($"{Path.GetFileName(path)}: {changedHashes} of {legalNow.Count} legal-move hashes differ" + (firstDrift < 0 ? "" : $", first at step {firstDrift}")
            + $"; fingerprint {(fingerprint == replay.EngineFingerprint ? "unchanged" : "changed")}");
        foreach (string problem in problems) Console.WriteLine("  PROBLEM " + problem);
        foreach (string change in behaviourChanges) Console.WriteLine("  BEHAVIOUR " + change);
        foreach (var change in drift)
            Console.WriteLine("  " + change.Key + ": " + change.Value.Select(v => v.Split(' ')[0]).Distinct().Count() + " steps, " + change.Value.Count + " changes, first " + change.Value[0]);
        if (changedHashes == 0 && fingerprint == replay.EngineFingerprint && problems.Count == 0 && behaviourChanges.Count == 0 && drift.Count == 0) return 0;
        if (problems.Count != 0) { Console.Error.WriteLine("refusing to write: the recording no longer replays as recorded"); return 1; }
        if (baseline == null) Console.WriteLine("  no --baseline: run --dump on the last passing commit to see what drifted");
        if (!write) return 1;
        var labels = drift.Keys.ToArray();
        if (!new HashSet<string>((accept ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries)).SetEquals(labels))
        { Console.Error.WriteLine("refusing to write: --accept must name exactly: " + string.Join(";", labels)); return 1; }
        if (behaviourChanges.Count != 0 && !Regex.IsMatch(behaviour ?? "", "^[0-9a-f]{7,40}: \\S.{9,}$"))
        { Console.Error.WriteLine("refusing to write: " + string.Join("; ", behaviourChanges) + " change the game; a deliberate change needs --accept-behaviour \"SHA: reason\""); return 1; }
        var legalPattern = new Regex("\"legalShas\":\\[[^\\]]*\\]");
        if (!legalPattern.IsMatch(text) || !text.Contains($"\"engineFingerprint\":\"{replay.EngineFingerprint}\"", StringComparison.Ordinal))
        { Console.Error.WriteLine("refusing to write: the replay file is not in the expected compact form"); return 1; }
        string renewed = legalPattern.Replace(text, "\"legalShas\":[" + string.Join(",", legalNow.Select(h => "\"" + h + "\"")) + "]", 1);
        renewed = renewed.Replace($"\"engineFingerprint\":\"{replay.EngineFingerprint}\"", $"\"engineFingerprint\":\"{fingerprint}\"", StringComparison.Ordinal);
        if (behaviourChanges.Count != 0) // accepted with a reason: the stored outcome follows
            renewed = RewriteGameOutcome(renewed, current.Turns, current.Winner);
        File.WriteAllText(path, renewed);
        Console.WriteLine($"rewrote {changedHashes} legal-move hashes and the fingerprint ({replay.EngineFingerprint} -> {fingerprint}); actions and decks untouched" + (behaviourChanges.Count != 0 ? "; " + string.Join(", ", behaviourChanges) + " accepted" : "; turns and winner untouched"));
        return 0;
    }

    // Proof of the tool itself, on temporary copies of a pinned replay that stores a fingerprint and still matches: an illegal recorded action and a
    // changed winner, unlabelled state and unaccepted legal hashes are refused without writes; accepted hash/fingerprint renewal preserves every other byte.
    internal static void RenewSelfTest()
    {
        string source = Directory.GetFiles("tests/transport-invariants/replays", "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal)
            .First(f => !f.Contains("transport-debt") && File.ReadAllText(f).Contains("\"engineFingerprint\":\"") && !File.ReadAllText(f).Contains("\"engineFingerprint\":null")
                && Renew(new[] { "--renew-replay", f }) == 0);
        string original = File.ReadAllText(source);
        string dir = Path.Combine(Path.GetTempPath(), "renew-replay-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try
        {
            string baseline = Path.Combine(dir, "baseline");
            Check(Renew(new[] { "--renew-replay", source, "--dump", baseline }) == 0, "renew: a matching replay dumps and reports no drift");
            string temp = Path.Combine(dir, "copy.json");
            var actions = new Regex("\"actions\":\\[(\\d+),").Match(original);
            Check(actions.Success, "renew: replay has an action list");
            File.WriteAllText(temp, original.Replace(actions.Value, "\"actions\":[9999,"));
            string illegal = File.ReadAllText(temp);
            Check(Renew(new[] { "--renew-replay", temp, "--baseline", baseline, "--accept", "", "--write" }) == 1 && File.ReadAllText(temp) == illegal, "renew: an illegal recorded action is refused and nothing is written");
            string fingerprint = new Regex("\"engineFingerprint\":\"([0-9a-f]+)\"").Match(original).Groups[1].Value;
            string firstHash = new Regex("\"legalShas\":\\[\"([0-9a-f]+)\"").Match(original).Groups[1].Value;
            string hashOnly = original.Replace("\"legalShas\":[\"" + firstHash, "\"legalShas\":[\"" + new string('1', firstHash.Length));
            File.WriteAllText(temp, hashOnly);
            Check(Renew(new[] { "--renew-replay", temp, "--baseline", baseline, "--accept", "", "--write" }) == 1
                && File.ReadAllText(temp) == hashOnly, "renew: unaccepted legal-move-only drift is refused without writing");
            Check(Renew(new[] { "--renew-replay", temp, "--baseline", baseline, "--accept", "legal moves", "--write" }) == 0
                && File.ReadAllText(temp) == original, "renew: legal-move-only drift requires its label");
            string firstStep = Path.Combine(baseline, "000.txt"), snapshot = File.ReadAllText(firstStep);
            string state = snapshot.Split('\n', 3)[2];
            string[] unlabelledStates = {
                state + "|history-facts-v1:unaccepted\n",
                state.Replace("}\n", "}|location:1:2:0:3\n"),
                state.Replace("}\n", "}|quest-rewards:[]\n"),
                state + "{unparsed entity}\n",
                new Regex("\"Zone\":\\d+").Replace(state, "\"Zone\":999", 1),
                new Regex("\"Controller\":\\d+").Replace(state, "\"Controller\":999", 1)
            };
            File.WriteAllText(temp, original);
            foreach (string changedState in unlabelledStates)
            {
                Check(changedState != state && StateDiff(state, changedState).Any(p => p.Key == "unlabelled state"),
                    "renew: history/location/quest rewards/unparsed entity/Zone/Controller drift has an acceptance label");
                File.WriteAllText(firstStep, string.Join("\n", snapshot.Split('\n', 3).Take(2)) + "\n" + changedState);
                Check(Renew(new[] { "--renew-replay", temp, "--baseline", baseline, "--accept", "", "--write" }) == 1
                    && File.ReadAllText(temp) == original, "renew: unaccepted state drift is refused and nothing is written");
            }
            File.WriteAllText(firstStep, snapshot);
            Check(StateDiff("{unparsed old}\n", "{unparsed new}\n").Any(p => p.Key == "unlabelled state"),
                "renew: changes between unparsed entity lines cannot disappear");
            Check(StateDiff("{\"Id\":1,\"CardId\":\"x\",\"Tags\":{\"1\":1}}\n|history-facts-v1:old",
                "{\"Id\":1,\"CardId\":\"x\",\"Tags\":{\"1\":2}}\n|history-facts-v1:new").Select(p => p.Key)
                .ToHashSet().SetEquals(new[] { "x tag 1", "unlabelled state" }), "renew: labelled tags cannot mask unlabelled drift");
            const string nested = "{\"note\":\"é\",\"turns\":1,\"winner\":2,\"game\":{\"nested\":{\"turns\":3,\"winner\":4},\"turns\":5,\"winner\":6},\"tail\":{\"turns\":7,\"winner\":8}}";
            Check(RewriteGameOutcome(nested, 50, 60) == nested.Replace("\"turns\":5", "\"turns\":50").Replace("\"winner\":6", "\"winner\":60"),
                "renew/rerecord: outcome rewrite touches only direct game properties, including with UTF-8 text");
            string tampered = original.Replace(fingerprint, new string('0', fingerprint.Length)).Replace("\"legalShas\":[\"" + firstHash, "\"legalShas\":[\"" + new string('1', firstHash.Length));
            File.WriteAllText(temp, tampered);
            Check(Renew(new[] { "--renew-replay", temp }) == 1, "renew: tampered hashes are reported as drift without --write");
            Check(Renew(new[] { "--renew-replay", temp, "--baseline", baseline, "--accept", "legal moves;unlabelled state", "--write" }) == 0, "renew: tampered hashes are rewritten");
            Check(File.ReadAllText(temp) == original, "renew: only the hashes and the fingerprint were rewritten (the file is byte-identical to the original again)");
            var winner = new Regex("\"winner\":(\\d+)").Match(original);
            Check(winner.Success, "renew: replay stores a winner");
            File.WriteAllText(temp, original.Replace(winner.Value, "\"winner\":" + (winner.Groups[1].Value == "1" ? 2 : 1)));
            string changedWinner = File.ReadAllText(temp);
            Check(Renew(new[] { "--renew-replay", temp, "--baseline", baseline, "--accept", "", "--write" }) == 1 && File.ReadAllText(temp) == changedWinner, "renew: a changed winner is refused without --accept-behaviour");
        }
        finally { Directory.Delete(dir, true); }
        Console.WriteLine("PASS pinned replay renewal tool: refuses illegal actions, changed winners and unaccepted state/legal drift; confines outcome rewrites to game");
    }

    // `--rerecord-replay PATH --baseline DIR --expect-first-diff STEP [--dump DIR] [--write]`: for a replay whose game really changes after an intended rules change.
    // It regenerates the game from the file's own identity (pinned decks, pool snapshot, seed, random policy; refuses if any cannot be rebuilt)
    // with the same policy stream the recorder uses, and prints the new outcome. Before STEP the baseline dump (from --renew-replay --dump on the
    // last passing commit) must match exactly: chosen move, legal-move hash, RNG state and full state; the first difference must be at STEP.
    // --write replaces `actions`, `legalShas`, game-record `turns`/`winner` and `engineFingerprint` only.
    // It leaves `findingsSha` unchanged: validate packet findings with the client-tier replay check afterward.
    internal static int Rerecord(string[] args)
    {
        string path = Path.GetFullPath(args[Array.IndexOf(args, "--rerecord-replay") + 1]);
        string baseline = Opt(args, "--baseline"), dump = Opt(args, "--dump");
        if (baseline == null || !int.TryParse(Opt(args, "--expect-first-diff"), out int expected)) { Console.Error.WriteLine("--rerecord-replay needs --baseline DIR and --expect-first-diff STEP"); return 1; }
        if (dump != null) Directory.CreateDirectory(dump);
        string text = File.ReadAllText(path);
        var replay = JsonSerializer.Deserialize<PinnedReplay>(text, Json);
        var old = replay.Game;
        if (old.Policy != "random") { Console.Error.WriteLine("refusing: only the random policy can be regenerated"); return 1; }
        var game = Simulation.Create(old, replay: true, playerDeck: replay.PlayerDeck, opponentDeck: replay.OpponentDeck);
        var policy = new PolicyRandom(unchecked((ulong)(long)old.Seed) ^ 0x485353494DUL);
        var actions = new List<int>(); var hashes = new List<string>();
        int step = 0, firstDiff = -1;
        var report = new List<string>();
        while (game.State != State.COMPLETE)
        {
            if (game.Turn > 200 || step >= 20000) { Console.Error.WriteLine("refusing: the regenerated game does not terminate"); return 1; }
            var legal = Simulation.Legal(game);
            string hash = Canonical.Sha(Canonical.Value(legal.Select(Simulation.TaskKey).ToArray()));
            int index = policy.Index(legal.Count);
            string snapshot = "action:" + Simulation.TaskKey(legal[index]) + "\nrng:" + Canonical.Value(game.Random.CaptureState()) + "\n" + Canonical.State(game);
            if (dump != null) File.WriteAllText(Path.Combine(dump, step.ToString("000") + ".txt"), snapshot);
            string basePath = Path.Combine(baseline, step.ToString("000") + ".txt");
            bool same = File.Exists(basePath) && File.ReadAllText(basePath) == snapshot
                && step < old.LegalShas.Count && old.LegalShas[step] == hash && step < old.Actions.Count && old.Actions[step] == index;
            if (!same && firstDiff < 0)
            {
                firstDiff = step;
                if (File.Exists(basePath))
                {
                    string[] a = File.ReadAllText(basePath).Split('\n', 3), b = snapshot.Split('\n', 3);
                    report.Add($"first difference at step {step}: move {a[0]} -> {b[0]}; RNG {(a[1] == b[1] ? "same" : "differs")}; legal hash {(step < old.LegalShas.Count && old.LegalShas[step] == hash ? "same" : "differs")}; recorded index {old.Actions.ElementAtOrDefault(step)} -> {index}");
                    foreach (var change in StateDiff(a[2], b[2])) report.Add("  " + change.Key + ": " + change.Value);
                }
            }
            actions.Add(index); hashes.Add(hash);
            if (!Simulation.Process(game, legal[index])) { Console.Error.WriteLine($"refusing: regenerated action {step} rejected"); return 1; }
            step++;
        }
        var current = new GameRecord(); Simulation.Finish(current, game);
        string fingerprint = Fingerprint(game);
        Console.WriteLine($"{Path.GetFileName(path)}: regenerated {actions.Count} actions (recorded {old.Actions.Count}); turns {old.Turns} -> {current.Turns}; winner {old.Winner} -> {current.Winner}");
        Console.WriteLine($"steps 0..{(firstDiff < 0 ? actions.Count : firstDiff) - 1} identical to the old recording (chosen move, legal-move hash, RNG state, full state)");
        foreach (string line in report) Console.WriteLine(line);
        if (firstDiff != expected) { Console.Error.WriteLine($"refusing: the first difference is at step {firstDiff}, expected {expected}"); return 1; }
        if (!args.Contains("--write")) return 1;
        var pattern = new Regex("\"actions\":\\[[^\\]]*\\]"); var legalPattern = new Regex("\"legalShas\":\\[[^\\]]*\\]");
        if (!pattern.IsMatch(text) || !legalPattern.IsMatch(text) || !text.Contains($"\"engineFingerprint\":\"{replay.EngineFingerprint}\"", StringComparison.Ordinal))
        { Console.Error.WriteLine("refusing: the replay file is not in the expected compact form"); return 1; }
        string renewed = pattern.Replace(text, "\"actions\":[" + string.Join(",", actions) + "]", 1);
        renewed = legalPattern.Replace(renewed, "\"legalShas\":[" + string.Join(",", hashes.Select(h => "\"" + h + "\"")) + "]", 1);
        renewed = RewriteGameOutcome(renewed, current.Turns, current.Winner);
        renewed = renewed.Replace($"\"engineFingerprint\":\"{replay.EngineFingerprint}\"", $"\"engineFingerprint\":\"{fingerprint}\"", StringComparison.Ordinal);
        File.WriteAllText(path, renewed);
        Console.WriteLine($"rewrote actions ({old.Actions.Count} -> {actions.Count}), legal-move hashes, turns, winner and fingerprint; id, decks, pool, seed and policy untouched");
        return 0;
    }

    private static string RewriteGameOutcome(string text, int turns, int winner)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        var reader = new Utf8JsonReader(bytes);
        var edits = new List<(int Start, int Length, string Value)>();
        bool inGame = false;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 1 && reader.ValueTextEquals("game"))
            { reader.Read(); inGame = reader.TokenType == JsonTokenType.StartObject; }
            else if (inGame && reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == 1) break;
            else if (inGame && reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 2
                && (reader.ValueTextEquals("turns") || reader.ValueTextEquals("winner")))
            {
                int value = reader.ValueTextEquals("turns") ? turns : winner;
                reader.Read(); reader.GetInt32();
                edits.Add((Encoding.UTF8.GetCharCount(bytes.AsSpan(0, (int)reader.TokenStartIndex)),
                    reader.ValueSpan.Length, value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
        }
        if (edits.Count != 2) throw new InvalidOperationException("the game record must contain turns and winner");
        foreach (var edit in edits.OrderByDescending(e => e.Start))
            text = text.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Value);
        return text;
    }

    // Known entity changes have precise labels; every remaining canonical-state difference requires "unlabelled state".
    internal static IEnumerable<KeyValuePair<string, string>> StateDiff(string before, string after)
    {
        static Dictionary<int, (string Card, JsonElement Tags)> Entities(string text, out string unlabelled)
        {
            var map = new Dictionary<int, (string, JsonElement)>();
            var remaining = new List<string>();
            foreach (string line in text.Split('\n'))
            {
                if (!line.StartsWith('{')) { remaining.Add(line); continue; }
                string json = line; int bar = json.IndexOf('|'); if (bar > 0) json = json[..bar];
                try
                {
                    using var document = JsonDocument.Parse(json);
                    var root = document.RootElement;
                    var tags = root.GetProperty("Tags");
                    if (tags.ValueKind != JsonValueKind.Object) throw new JsonException("entity tags must be an object");
                    map.Add(root.GetProperty("Id").GetInt32(), (root.TryGetProperty("CardId", out var card) ? card.GetString() : null, tags.Clone()));
                    remaining.Add(string.Join(",", root.EnumerateObject().Where(p => p.Name != "CardId" && p.Name != "Tags")) + line[json.Length..]);
                }
                catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException or ArgumentException)
                { remaining.Add(line); }
            }
            unlabelled = string.Join("\n", remaining);
            return map;
        }
        var a = Entities(before, out string unlabelledBefore); var b = Entities(after, out string unlabelledAfter);
        var result = new List<KeyValuePair<string, string>>();
        foreach (int id in a.Keys.Union(b.Keys).OrderBy(i => i))
        {
            bool inA = a.TryGetValue(id, out var x), inB = b.TryGetValue(id, out var y);
            if (!inA || !inB) { result.Add(new("entity " + (inA ? "removed" : "added"), $"entity {id} {(inA ? x.Card : y.Card)}")); continue; }
            if (x.Card != y.Card) result.Add(new("entity card", $"entity {id} {x.Card} -> {y.Card}"));
            var ta = x.Tags.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ToString());
            var tb = y.Tags.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.ToString());
            foreach (string tag in ta.Keys.Union(tb.Keys).OrderBy(t => t, StringComparer.Ordinal))
            {
                ta.TryGetValue(tag, out string va); tb.TryGetValue(tag, out string vb);
                if (va != vb) result.Add(new($"{y.Card} tag {tag}", $"entity {id} {va ?? "absent"} -> {vb ?? "absent"}"));
            }
        }
        if (unlabelledBefore != unlabelledAfter || before != after && result.Count == 0)
            result.Add(new("unlabelled state", "canonical state outside parsed entity cards/tags differs"));
        return result;
    }
}
