// Reward V3 roll function (owner 2026-10-25), retired as a match reward (profiles now issue V4-V6, see RewardV5Checks): losses pay nothing; a win rolls 50% gold 40-80, 25% dust 40-80, 25% both 30-60.
internal static class RewardV3Checks
{
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); }
    private static Guid Id(int i) { var b = new byte[16]; BitConverter.GetBytes(i * 2654435761L).CopyTo(b, 0); BitConverter.GetBytes((long)i).CopyTo(b, 8); return new Guid(b); }
    private static LocalReward Roll(Guid id, int seed, LocalRewardFacts facts = null) =>
        LocalEconomy.RewardV3("Won", 12, false, facts ?? new LocalRewardFacts(), LocalEconomy.RewardRollSeed(id.ToString(), seed));
    private static int Kind(LocalReward r) => r.Gold > 0 && r.Dust > 0 ? 3 : r.Gold > 0 ? 0 : 2;

    internal static void Run()
    {
        var none = new LocalRewardFacts();
        int seedRoot = LocalEconomy.RewardRollSeed(Id(1).ToString(), 7);
        foreach (var gated in new[] { ("Lost", 12, false), ("Lost", 12, true), ("Tied", 12, false), ("Abandoned", 12, false), ("Won", 3, false) })
        {
            var r = LocalEconomy.RewardV3(gated.Item1, gated.Item2, gated.Item3,
                new LocalRewardFacts { DistinctCardsPlayed = 9, EnemyMinionsDefeated = 9, MinimumHealth = 5 }, seedRoot);
            Check(r.Gold == 0 && r.Dust == 0 && r.BonusGold == 0 && !r.PackReward, "V3 pays nothing for " + gated);
        }

        // Distribution over many match ids and seeds: shares, inclusive ranges and the documented expected value.
        const int samples = 40000;
        var counts = new int[4]; long total = 0;
        int goldMin = int.MaxValue, goldMax = 0, dustMin = int.MaxValue, dustMax = 0, bothMin = int.MaxValue, bothMax = 0;
        for (int i = 0; i < samples; i++)
        {
            var r = Roll(Id(i), i % 977); int kind = Kind(r); counts[kind]++; total += r.Gold + r.Dust;
            Check(!r.PackReward && r.BonusGold == 0, "no-bonus win has no pack or bonus");
            if (kind == 0) { goldMin = Math.Min(goldMin, r.Gold); goldMax = Math.Max(goldMax, r.Gold); }
            else if (kind == 2) { dustMin = Math.Min(dustMin, r.Dust); dustMax = Math.Max(dustMax, r.Dust); }
            else { bothMin = Math.Min(bothMin, Math.Min(r.Gold, r.Dust)); bothMax = Math.Max(bothMax, Math.Max(r.Gold, r.Dust)); }
        }
        Check(Math.Abs(counts[0] / (double)samples - 0.50) < 0.015 && Math.Abs(counts[2] / (double)samples - 0.25) < 0.015
            && Math.Abs(counts[3] / (double)samples - 0.25) < 0.015 && counts[1] == 0, "V3 roll shares 50/25/25: " + string.Join(",", counts));
        Check(goldMin == 40 && goldMax == 80 && dustMin == 40 && dustMax == 80 && bothMin == 30 && bothMax == 60, "V3 inclusive ranges");
        Check(Math.Abs(total / (double)samples - 67.5) < 1.0, "V3 expected win ~67.5: " + total / (double)samples);

        // Determinism: the roll is a pure function of (Id, Seed); the Guid's text format does not matter.
        var id = Id(42);
        Check(LocalEconomy.RewardRollSeed(id.ToString("D"), 3) == LocalEconomy.RewardRollSeed(id.ToString("N"), 3)
            && LocalEconomy.RewardRollSeed(id.ToString(), 3) == LocalEconomy.RewardRollSeed(id.ToString().ToUpperInvariant(), 3), "roll ignores Guid format");
        Check(Enumerable.Range(0, 5).Select(_ => Roll(id, 3)).Select(r => r.Gold + ":" + r.Dust).Distinct().Count() == 1, "V3 roll deterministic");
        Check(Enumerable.Range(0, 64).Select(i => Roll(id, i)).Select(r => r.Gold + ":" + r.Dust).Distinct().Count() > 20, "seed changes the roll");

        // Bonus: half V2 weights, to gold when the roll paid gold, else to dust; pack at bonus >= 10.
        var great = new LocalRewardFacts { DistinctCardsPlayed = 6, EnemyMinionsDefeated = 5, MinimumHealth = 5 };
        int dustOnly = Enumerable.Range(0, 200).First(i => Kind(Roll(Id(i), 1)) == 2);
        int goldOnly = Enumerable.Range(0, 200).First(i => Kind(Roll(Id(i), 1)) == 0);
        int both = Enumerable.Range(0, 200).First(i => Kind(Roll(Id(i), 1)) == 3);
        var d0 = Roll(Id(dustOnly), 1); var d1 = Roll(Id(dustOnly), 1, great);
        Check(d1.Gold == 0 && d1.Dust == d0.Dust + 20 && d1.BonusGold == 20 && d1.PackReward, "bonus lands on dust-only roll");
        var g0 = Roll(Id(goldOnly), 1); var g1 = Roll(Id(goldOnly), 1, great);
        Check(g1.Dust == 0 && g1.Gold == g0.Gold + 20 && g1.PackReward, "bonus lands on gold-only roll");
        var b0 = Roll(Id(both), 1); var b1 = Roll(Id(both), 1, great);
        Check(b1.Gold == b0.Gold + 20 && b1.Dust == b0.Dust && b1.PackReward, "bonus lands on gold of a both roll");
        Check(Roll(Id(goldOnly), 1, new LocalRewardFacts { MinimumHealth = 10 }).PackReward
            && Roll(Id(goldOnly), 1, new LocalRewardFacts { DistinctCardsPlayed = 6, EnemyMinionsDefeated = 5 }).PackReward
            && !Roll(Id(goldOnly), 1, new LocalRewardFacts { DistinctCardsPlayed = 6 }).PackReward
            && Roll(Id(goldOnly), 1, new LocalRewardFacts { DistinctCardsPlayed = 6 }).BonusGold == 5, "great win pack rule");

        PackSetChecks();
        Console.WriteLine("PASS reward V3 roll function (historical; V4 keeps its base roll): loss pays 0, win roll 50/25/25 in range, EV 67.5, deterministic, bonus/pack, weighted pack-set draw");
    }

    private static void PackSetChecks()
    {
        var weights = new List<KeyValuePair<string, int>> { new("A", 300), new("DONE", 0), new("B", 100) };
        var drawn = new Dictionary<string, int> { ["A"] = 0, ["B"] = 0, ["DONE"] = 0 };
        for (int i = 0; i < 8000; i++)
        {
            int seed = LocalEconomy.RewardRollSeed(Id(i).ToString(), i);
            string set = LocalEconomy.RewardPackSet(seed, weights, "NEW");
            Check(set == LocalEconomy.RewardPackSet(seed, weights, "NEW"), "pack set deterministic");
            drawn[set]++;
        }
        Check(drawn["DONE"] == 0 && Math.Abs(drawn["A"] / 8000.0 - 0.75) < 0.03, "pack set weights: " + drawn["A"] + "/" + drawn["B"]);
        Check(LocalEconomy.RewardPackSet(1, new List<KeyValuePair<string, int>> { new("A", 0), new("B", 0) }, "NEW") == "NEW", "all complete falls back");
    }
}
