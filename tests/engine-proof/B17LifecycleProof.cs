using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using static Core2026Proof;

internal static class B17LifecycleProof
{
    [GroupProof("b17-lifecycle")]
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (var test in B17LifecycleCardsProof.Scenarios)
        {
            try {
                var g = DeathKnightC1Proof.NewGame(1);
                PoolPilotProof.EmptyHand(g.Player1);
                test.Value(g, g.Player1, test.Key);
            } catch (Exception e) { failures.Add(test.Key + ": " + e.Message); }
        }
        Core2026Proof.Check(failures.Count == 0, string.Join("; ", failures));
        B17LifecycleEdgesProof.Run();
        DeferredHistoryCardsProof.Run();
        Console.WriteLine("PASS b17 lifecycle facility");
    }
}
