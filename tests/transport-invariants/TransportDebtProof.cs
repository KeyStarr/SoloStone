using System.Text.Json;

namespace TransportInvariants;

internal static class TransportDebtProof
{
    internal static void Run(string only = null)
    {
        if (only == "shapes") HistoryShapeProof.DebtShapes();
        foreach (string name in new[] { "helper-accepted", "runtime", "rewind-actor" })
        {
            if (only != null && name != only) continue;
            var replay = JsonSerializer.Deserialize<TransportReplay>(File.ReadAllText(
                $"tests/transport-invariants/replays/transport-debt/{name}.json"), Program.Json);
            var runner = new Runner();
            runner.Run(replay);
            if (name == "rewind-actor")
                SelfChecks.Check(runner.Rewinds > 0, "rewind actor regression restores the discarded timeline");
            SelfChecks.Check(runner.Completed.Values.Sum() == 1 && runner.ViolationCount == 0,
                name + " transport debt regression: " + string.Join("; ", runner.Buckets.Keys));
            Console.WriteLine("PASS transport debt " + name);
        }
    }
}
