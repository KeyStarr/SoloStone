using System.Text.Json;
using System.Text.Json.Serialization;
using HsSim;
using SabberStoneCore.Enums;

// Engine half of the pinned transport replays (tests/transport-invariants/replays). Moved from the transport Runner (verify mode):
// replay the recorded actions with Simulation.Create/Legal/Process and require the recorded legal-move hash at every step and, where the
// replay stores one, the state/history/RNG fingerprint. The pinned pool snapshot comes from tests/goldens/pools. Needs no client.
// The packet half (transport parity, history shape, the witnesses that read packets) stays in transport-invariants. The three
// transport-debt replays record no actions and are not covered here.
internal static class PinnedReplayEngineProof
{
    private sealed class PinnedReplay
    {
        public int Schema { get; set; } = 1;
        public GameRecord Game { get; set; }
        public DeckSpec PlayerDeck { get; set; }
        public DeckSpec OpponentDeck { get; set; }
        public string EngineFingerprint { get; set; }
        public bool Focused { get; set; }
        public string FindingsSha { get; set; }
    }

    private static readonly JsonSerializerOptions Json = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException("SELF-CHECK: " + message);
    }

    internal static void Run()
    {
        const string folder = "tests/transport-invariants/replays";
        int replays = 0, fingerprints = 0;
        var drifted = new List<string>();
        foreach (string kind in new[] { "deathrattle", "meta-pre-be", "private-return" })
        {
            var files = Directory.GetFiles($"{folder}/{kind}", "*.json").Order(StringComparer.Ordinal).ToArray();
            Check(files.Length > 0, $"pinned {kind} replays exist");
            foreach (string path in files)
            {
                var replay = JsonSerializer.Deserialize<PinnedReplay>(File.ReadAllText(path), Json);
                string name = $"{kind}/{Path.GetFileName(path)}";
                Check(replay.PlayerDeck?.Cards.Length == 30 && replay.OpponentDeck?.Cards.Length == 30,
                    name + " pins both original stress decks independently of registry growth");
                if (kind == "deathrattle")
                    Check(!string.IsNullOrEmpty(replay.EngineFingerprint), name + " pins engine state/history/RNG");
                // Name the replay: a bare "fingerprint changed" does not say which one moved (all are listed before failing).
                try { Replay(replay); }
                catch (InvalidOperationException error) { drifted.Add(name + ": " + error.Message); continue; }
                replays++;
                if (replay.EngineFingerprint != null) fingerprints++;
            }
        }
        Check(drifted.Count == 0, "pinned replays failed on the engine: " + string.Join("; ", drifted));
        Console.WriteLine($"PASS pinned replays on the engine: {replays} replays, legal-move hash chain and actions at every step, {fingerprints} state/history/RNG fingerprints");
    }

    private static void Replay(PinnedReplay replay)
    {
        var record = replay.Game;
        if (record.Policy != "random") throw new ArgumentException("transport policy must be random");
        var game = Simulation.Create(record, replay: true, playerDeck: replay.PlayerDeck, opponentDeck: replay.OpponentDeck);
        int step = 0;
        while (game.State != State.COMPLETE)
        {
            if (game.Turn > 200 || step >= 20000) throw new InvalidOperationException("transport game exceeded termination bounds: " + record.Id);
            var legal = Simulation.Legal(game);
            if (legal.Count == 0) throw new InvalidOperationException("transport legal tasks empty: " + record.Id);
            string hash = Canonical.Sha(Canonical.Value(legal.Select(Simulation.TaskKey).ToArray()));
            if (step >= record.LegalShas.Count || hash != record.LegalShas[step])
                throw new InvalidOperationException($"replay.legal: {record.Id} checkpoint {step}");
            if (step >= record.Actions.Count || record.Actions[step] < 0 || record.Actions[step] >= legal.Count)
                throw new InvalidOperationException($"replay.actions: {record.Id} checkpoint {step}");
            var task = legal[record.Actions[step]];
            try { Simulation.Process(game, task); }
            catch (Exception error) { throw new InvalidOperationException($"transport action failed: {record.Id} step={step} task={Simulation.TaskKey(task)}", error); }
            step++;
        }
        // A normal game must keep its achievement evidence; fail-open recording must not hide recorder regressions.
        if (!game.HistoryFacts.EvidenceValid)
            throw new InvalidOperationException($"history.evidence-invalid: {record.Id}: {game.HistoryFacts.EvidenceFailure}");
        if (step != record.Actions.Count || step != record.LegalShas.Count)
            throw new InvalidOperationException("replay.result: action length changed");
        if (replay.EngineFingerprint != null)
        {
            string fingerprint = Canonical.Sha(Canonical.State(game) + Canonical.History(game)
                + Canonical.Value(game.Random.CaptureState()));
            if (replay.EngineFingerprint != fingerprint)
                throw new InvalidOperationException("replay.engine: state/history/RNG fingerprint changed");
        }
    }
}
