using System.Text.Json;

namespace TransportInvariants;

internal static class MetaPreBeProof
{
    internal static int Run(string only = null)
    {
        int failures = 0;
        foreach (string name in new[] { "ice-barrier", "TIME_876e", "CORE_BT_416", "EDR_226", "TIME_704", "TLC_100t3" })
        {
            if (only != null && name != only) continue;
            try
            {
                var replay = JsonSerializer.Deserialize<TransportReplay>(File.ReadAllText(
                    $"tests/transport-invariants/replays/meta-pre-be/{name}.json"), Program.Json);
                var runner = new Runner();
                SelfChecks.Check(replay.PlayerDeck?.Cards.Length == 30 && replay.OpponentDeck?.Cards.Length == 30,
                    name + " retains original stress decks independently of registry growth");
                var shape = runner.Run(replay, verify: true);
                if (name == "TLC_100t3")
                    SelfChecks.Check(shape.Findings.Any(f => f.Rule == "SPLIT_EFFECT_BLOCK" && f.Card == name
                        && f.Detail == "sibling-POWER under=PLAY header=differs meta=DAMAGE"
                        && f.Conditions?.Contains(HistoryShapeCondition.ExhaustionDeathrattle) == true),
                        name + " retains the original exhaustion Deathrattle regression witness");
                SelfChecks.Check(runner.Failures == 0 && runner.ViolationCount == 0, name + " transport clean");
                Console.WriteLine("PASS meta-pre-be replay " + name);
            }
            catch (Exception error) { failures++; Console.Error.WriteLine("FAIL meta-pre-be " + name + ": " + error); }
        }
        bool shapes = HistoryShapeReport.Write(false);
        return failures != 0 ? 2 : shapes ? 0 : 1;
    }
}
