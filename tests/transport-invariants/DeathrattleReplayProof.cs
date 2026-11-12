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
        SelfChecks.Check(runner.ViolationCount == 0 && runner.Completed.Values.Sum() == 1 && ExpectedFindingsOnly(history),
            "Felhunter empty Deathrattle, granted Deathrattle and Lifesteal retain distinct keyword boundaries: "
            + string.Join("; ", history.Findings.Select(f => $"{f.Rule}@{f.Step}: {f.Card}/{f.BlockCard} {f.Detail}")));
        Console.WriteLine("PASS Felhunter exact replay: legal hashes/actions, state/history/RNG fingerprint, transport parity and history shape");
    }
    // The re-recorded game (the old one stopped matching after the Bloated Leech fix) produces exactly one history-shape finding, an
    // exhaustion Deathrattle of a composite location, which the baseline accepts. Anything else, info findings included, fails.
    private static bool ExpectedFindingsOnly(HistoryShape history) =>
        history.Findings.Count == 1 && history.Findings[0] is { Rule: "SPLIT_EFFECT_BLOCK", Severity: "error", Card: "TLC_100t1" } finding
            && finding.Detail == "sibling-POWER under=PLAY header=differs meta=DAMAGE"
            && finding.Conditions?.SequenceEqual(new[] { HistoryShapeCondition.ExhaustionDeathrattle }) == true;
}
