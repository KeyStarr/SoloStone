using System.Text.Json;

namespace TransportInvariants;

internal static class DeathrattleReplayProof
{
    internal static void Run()
    {
        const string path = "tests/transport-invariants/replays/deathrattle/standard-batch-1-standard-batch-1-21-vs-standard-batch-1-25-s4-random.json";
        var replay = JsonSerializer.Deserialize<TransportReplay>(File.ReadAllText(path), Program.Json);
        SelfChecks.Check(replay.PlayerDeck?.Cards.Length == 30 && replay.OpponentDeck?.Cards.Length == 30
            && !string.IsNullOrEmpty(replay.EngineFingerprint), "Felhunter replay pins decks and engine state/history/RNG");
        var runner = new Runner();
        var history = runner.Run(replay, verify: true);
        SelfChecks.Check(runner.ViolationCount == 0 && runner.Completed.Values.Sum() == 1 && Unreviewed(history).Length == 0,
            "Felhunter empty Deathrattle, granted Deathrattle and Lifesteal retain distinct keyword boundaries: "
            + string.Join("; ", history.Findings.Select(f => $"{f.Rule}@{f.Step}: {f.Card}/{f.BlockCard} {f.Detail}")));
        Console.WriteLine("PASS Felhunter exact replay: legal hashes/actions, state/history/RNG fingerprint, transport parity and history shape");
    }
    // Findings the history-shape baseline has not reviewed (a reviewed, accepted shape is not a failure here).
    private static HistoryShapeBucket[] Unreviewed(HistoryShape history) =>
        HistoryShapeReport.Classify(history.Findings.Where(f => f.Severity == "error").GroupBy(f => (f.Rule, f.Card))
            .Select(group => HistoryShapeReport.Bucket(group, group.Key.Rule, group.Key.Card)).ToArray(),
            HistoryShapeReport.LoadBaseline(), fullCorpus: false).Fresh;
}
