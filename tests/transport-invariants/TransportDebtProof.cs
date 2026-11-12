using System.Text.Json;

namespace TransportInvariants;

// Pinned replays of strict-corpus games that failed transport checks before the October 2026 cleanup. Their decks
// are stored in the file so later registry growth cannot reshuffle them away from the failure. No actions are
// recorded: each game is re-run from deck and seed. helper-accepted (29 vs 32, seed 9): the native HIDE of a
// temporary helper (GBL_999e) kept its attachment/creator fields on the client (nine RETIRED_HELPER violations).
// runtime (41 vs 6, seed 2): a null dereference at step 94 after Shadow of Demise became a Trial form but copied
// only the definition, losing its two Kazakus components. rewind-actor (10 vs 37, seed 0): the test client model
// kept removed entities that native drops before a Rewind reset's FULLs.
internal static class TransportDebtProof
{
    internal static void Run(string only = null)
    {
        string[] replays = { "helper-accepted", "runtime", "rewind-actor" };
        // A name that matches nothing would run no check and still pass.
        if (only != null && only != "shapes" && !replays.Contains(only))
            throw new ArgumentException("unknown transport debt check: " + only + " (shapes, " + string.Join(", ", replays) + ")");
        if (only == "shapes") HistoryShapeProof.DebtShapes();
        foreach (string name in replays)
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
