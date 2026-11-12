using SabberStoneCore.CardSets.Client;
using static TransportInvariants.SelfChecks;

namespace TransportInvariants;

// The random stress corpus cannot reliably play every Standard card: class-gated cards can land in a deck whose hero
// never makes them legal (the first case: two Herald cards), and some need a specific board to be playable at all.
// Cards the corpus missed get a deterministic strict-transport game with a compatible setup. The per-card checks below
// pin the setups that need special handling: a friendly Wisp (EDR_804), a friendly Undead (CORE_EDR_002, Poison
// Breath), Armor to pay BE_042, a held Shadow of Demise (CORE_RLK_567, playable only after it turns into a spell
// already cast, here the Coin) and Osk (TLC_452).
internal static class StandardCoverageChecks
{
    internal static void Run(bool includeCorpus = true)
    {
        WispCoverage();
        FriendlyUndeadCoverage();
        ArmorCoverage();
        HeldSpellCoverage();
        HandEntryFormCoverage();
        if (!includeCorpus) return;
        var runner = new Runner();
        // Any registry member can become a random-game miss when batch membership changes.
        foreach (var replay in Runner.StandardCoverageCases(StandardSets.CollectibleIds))
        {
            try { runner.Run(replay); }
            catch (Exception error) { throw new InvalidOperationException("Standard coverage failed: " + replay.Game.Id, error); }
        }
        Check(runner.StandardBatchPlayed.SetEquals(StandardSets.CollectibleIds),
            "deterministic fallback plays every registered Standard card without random-game coverage; missing=" + string.Join(",", StandardSets.CollectibleIds.Except(runner.StandardBatchPlayed)));
        Check(runner.ViolationCount == 0 && runner.StandardBatchViolations == 0,
            "deterministic Standard coverage retains every transport invariant: " + string.Join("; ", runner.Buckets.Values.Select(
                b => $"{b.Card}/{b.Tag}: expected={b.Expected}, shown={b.Shown}, replay={b.Example.Replay}")));
        Check(runner.SuspendedAnswers > 0, "deterministic Standard coverage resolves suspended decisions");
        Console.WriteLine($"PASS Standard coverage: cards={runner.StandardBatchPlayed.Count}; both missed Heralds, minion targets, suspended decisions; strict transport");
    }

    private static void HandEntryFormCoverage()
    {
        var runner = new Runner();
        var replay = Runner.StandardCoverageCases(new[] { "TLC_452" }).Single();
        runner.Run(replay);
        runner.Run(replay, verify: true);
        Check(runner.StandardBatchPlayed.SetEquals(new[] { "TLC_452" }) && runner.ViolationCount == 0,
            "Osk forced collectible plays through its hand-entry form and replays with strict transport");
        Console.WriteLine("PASS hand-entry form coverage: Osk played and replayed; strict transport");
    }

    private static void HeldSpellCoverage()
    {
        var runner = new Runner();
        var replay = Runner.StandardCoverageCases(new[] { "CORE_RLK_567" }).Single();
        runner.Run(replay);
        runner.Run(replay, verify: true);
        Check(runner.StandardBatchPlayed.SetEquals(new[] { "CORE_RLK_567" })
            && runner.CardsPlayed.Contains("CORE_RLK_567") && runner.CardsPlayed.Contains("GAME_005")
            && runner.ViolationCount == 0, "held Shadow of Demise legally transforms, plays and replays with strict transport");
        Console.WriteLine("PASS held spell coverage: Shadow of Demise played as Coin and replayed; strict transport");
    }

    private static void WispCoverage()
    {
        var runner = new Runner();
        var replay = Runner.StandardCoverageCases(new[] { "EDR_804" }).Single();
        runner.Run(replay); runner.Run(replay,verify: true);
        Check(runner.StandardBatchPlayed.SetEquals(new[] { "EDR_804" }) && runner.ViolationCount == 0,
            "friendly Wisp fallback plays Divination and replays with strict transport");
        Console.WriteLine("PASS Wisp coverage: Divination played and replayed; strict transport");
    }

    private static void ArmorCoverage()
    {
        var runner = new Runner();
        var replay = Runner.StandardCoverageCases(new[] { "BE_042" }).Single();
        runner.Run(replay);
        runner.Run(replay, verify: true);
        Check(runner.StandardBatchPlayed.SetEquals(new[] { "BE_042" }) && runner.ViolationCount == 0,
            "Armor fallback funds Anvil of Reshaping and replays with strict transport");
        Console.WriteLine("PASS Armor coverage: Anvil of Reshaping played and replayed; strict transport");
    }

    private static void FriendlyUndeadCoverage()
    {
        var runner = new Runner();
        var replay = Runner.StandardCoverageCases(new[] { "CORE_EDR_002" }).Single();
        runner.Run(replay);
        runner.Run(replay, verify: true);
        Check(runner.StandardBatchPlayed.SetEquals(new[] { "CORE_EDR_002" }) && runner.ViolationCount == 0,
            "friendly Undead fallback plays Poison Breath with deterministic replay and strict transport");
        Console.WriteLine("PASS friendly Undead coverage: Poison Breath played and replayed; strict transport");
    }
}
