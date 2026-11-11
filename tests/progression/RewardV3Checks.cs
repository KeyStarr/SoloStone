using System.Xml.Serialization;

// Reward V3 (owner 2026-10-25): losses pay nothing; a win rolls 50% gold 40-80, 25% dust 40-80, 25% both 30-60.
internal static class RewardV3Checks
{
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); }
    private static void Reject(Action action, string name)
    {
        try { action(); }
        catch (Exception e) when (e is InvalidOperationException || e is ArgumentException || e is IOException || e is UnauthorizedAccessException) { return; }
        throw new Exception("Expected rejection: " + name);
    }
    private static LocalProfileData Read(string path) { using var s = File.OpenRead(path); return (LocalProfileData)new XmlSerializer(typeof(LocalProfileData)).Deserialize(s); }
    private static void Write(string path, LocalProfileData d) { using var s = File.Create(path); new XmlSerializer(typeof(LocalProfileData)).Serialize(s, d); }
    private static Guid Id(int i) { var b = new byte[16]; BitConverter.GetBytes(i * 2654435761L).CopyTo(b, 0); BitConverter.GetBytes((long)i).CopyTo(b, 8); return new Guid(b); }
    private static LocalReward Roll(Guid id, int seed, LocalRewardFacts facts = null) =>
        LocalEconomy.RewardV3("Won", 12, false, facts ?? new LocalRewardFacts(), LocalEconomy.RewardRollSeed(id.ToString(), seed));
    private static int Kind(LocalReward r) => r.Gold > 0 && r.Dust > 0 ? 3 : r.Gold > 0 ? 0 : 2;

    internal static void Run(string root)
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

        PackSetChecks(root);
        ProfileChecks(root);
        Console.WriteLine("PASS reward V3: loss pays 0, win roll 50/25/25 in range, EV 67.5, deterministic, bonus/pack, atomic dust settle, retry/restart, validation, V2 pending kept");
    }

    private static void PackSetChecks(string root)
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

        var sets = FairPackRules.PackTypes.Where(t => t != FairPackRules.Standard && t != FairPackRules.Legacy).ToList();
        var great = new LocalRewardFacts { DistinctCardsPlayed = 6, EnemyMinionsDefeated = 5, MinimumHealth = 5 };
        // Every set but one complete: that set always drops; then all complete: the newest set.
        string keep = sets.First(t => t != FairPackRules.NewestSet);
        foreach (bool allComplete in new[] { false, true })
        {
            string path = Path.Combine(root, "pack-set-" + allComplete + ".xml");
            using (var profile = new LocalProfile(path)) profile.Begin("mage", "priest", 3);
            var data = Read(path);
            foreach (string set in sets.Where(t => allComplete || t != keep))
                foreach (var card in FairPackRules.Eligible(set, FairPackRules.RuleVersion))
                {
                    var row = data.Collection.SingleOrDefault(c => c.Id == card.Id);
                    if (row == null) data.Collection.Add(row = new LocalCardCount { Id = card.Id });
                    row.Count = Math.Max(row.Count, LocalEconomy.PlayableCap(card)); row.Acquired = LocalEconomy.PlayableCap(card);
                }
            Write(path, data);
            using (var profile = new LocalProfile(path))
            {
                var won = profile.Settle(profile.Pending.Id, "Won", 12, false, great);
                Check(won.PackReward && won.Packs.Single().PackType == (allComplete ? FairPackRules.NewestSet : keep)
                    && profile.PackInventory.Single(p => p.PackType == won.Packs.Single().PackType).Count == 1, "V3 pack set completion rule " + allComplete);
            }
        }
        // V2 replay: a pending match begun on the old build keeps the newest set.
        string legacy = Path.Combine(root, "pack-set-v2.xml");
        using (var profile = new LocalProfile(legacy)) profile.Begin("mage", "priest", 3);
        var v2 = Read(legacy); v2.Pending.RewardVersion = 2; Write(legacy, v2);
        using (var profile = new LocalProfile(legacy))
            Check(profile.Settle(profile.Pending.Id, "Won", 12, false, great).Packs.Single().PackType == FairPackRules.NewestSet, "V2 pack stays newest set");
    }

    private static void ProfileChecks(string root)
    {
        string path = Path.Combine(root, "reward-v3.xml");
        var facts = new LocalRewardFacts { DistinctCardsPlayed = 7, EnemyMinionsDefeated = 1, MinimumHealth = 20 };
        LocalMatchRecord won;
        using (var profile = new LocalProfile(path))
        {
            profile.CreateDraft("V3", "HERO_08");
            // A loss: rank and class XP, no currency.
            var lost = profile.Begin("mage", "priest", 5);
            Check(lost.RewardVersion == 3 && Read(path).Pending.RewardVersion == 3, "new matches begin on reward V3");
            var lostResult = profile.Settle(lost.Id, "Lost", 12, false, facts);
            Check(lostResult.Gold == 0 && lostResult.DustReward == 0 && !lostResult.PackReward && profile.Gold == 0 && profile.DustBalance == 0
                && profile.ClassXp["HERO_08"] > 0 && lostResult.Ladder != null, "V3 loss: XP and rank only");
            var lossSummary = LocalRewardPresentation.Summary(lostResult, profile);
            Check(lossSummary.Items.Count == 0 && LocalRewardPresentation.NoItemsText(lossSummary).Contains("only victories"), "V3 loss summary");
            profile.AcknowledgeRewardPresentation(lost.Id);

            profile.Begin("mage", "priest", 0);
            profile.RecoverInterrupted();
            Check(profile.Gold == 0 && profile.DustBalance == 0 && profile.History.Last().DustReward == 0, "abandon pays no dust");
            // Pick the pending match's seed so its roll pays both currencies and the receipt exercises the dust path.
            profile.Begin("mage", "priest", 0);
        }
        var fixture = Read(path);
        fixture.Pending.Seed = Enumerable.Range(0, 400).First(s => Kind(LocalEconomy.RewardV3("Won", 12, false, facts,
            LocalEconomy.RewardRollSeed(fixture.Pending.Id, s))) == 3);
        Write(path, fixture);
        using (var profile = new LocalProfile(path))
        {
            var pending = profile.Pending;
            Check(pending.RewardVersion == 3 && pending.Seed == fixture.Pending.Seed, "edited pending reloads");
            long goldBefore = profile.Gold, dustBefore = profile.DustBalance;
            byte[] pendingBytes = File.ReadAllBytes(path);
            Directory.CreateDirectory(path + ".tmp");
            Reject(() => profile.Settle(pending.Id, "Won", 12, false, facts), "V3 settle commit failure");
            Check(profile.Gold == goldBefore && profile.DustBalance == dustBefore && profile.Pending.Id == pending.Id
                && File.ReadAllBytes(path).SequenceEqual(pendingBytes), "failed V3 settle changed gold or dust");
            Directory.Delete(path + ".tmp");
            won = profile.Settle(pending.Id, "Won", 12, false, facts);
            var expected = LocalEconomy.RewardV3("Won", 12, false, facts, LocalEconomy.RewardRollSeed(won.Id, won.Seed));
            Check(won.Gold == expected.Gold && won.DustReward == expected.Dust && won.BonusGold == 5 && !won.PackReward
                && profile.Gold == goldBefore + won.Gold && profile.DustBalance == dustBefore + won.DustReward, "V3 win receipt and balances");
            var retry = profile.Settle(pending.Id, "Won", 12, false, facts);
            Check(retry.Gold == won.Gold && retry.DustReward == won.DustReward && profile.Gold == goldBefore + won.Gold
                && profile.DustBalance == dustBefore + won.DustReward, "V3 settle retry is idempotent");
            Reject(() => profile.Settle(pending.Id, "Won", 12, false, new LocalRewardFacts()), "conflicting V3 retry");
            Check(Kind(expected) == 3, "fixture rolled both");
            {
                var summary = LocalRewardPresentation.Summary(won, profile);
                Check(summary.Items.Any(i => i.Kind == LocalRewardKind.Gold && i.Amount == won.Gold)
                    && summary.Items.Any(i => i.Kind == LocalRewardKind.Dust && i.Amount == won.DustReward)
                    && summary.Body.Contains("Total: " + won.Gold + " gold + " + won.DustReward + " dust")
                    && summary.Body.Contains("Bonus +5 gold"), "V3 summary lists gold and dust");
            }
        }
        // Restart mid-presentation: identical bytes, balances and pending presentation; a retry pays nothing more.
        byte[] settled = File.ReadAllBytes(path);
        using (var profile = new LocalProfile(path))
        {
            Check(profile.PendingRewardPresentation?.Id == won.Id && profile.PendingRewardPresentation.DustReward == won.DustReward
                && File.ReadAllBytes(path).SequenceEqual(settled), "restart keeps V3 presentation");
            long gold = profile.Gold, dust = profile.DustBalance;
            profile.Settle(won.Id, "Won", 12, false, facts);
            Check(profile.Gold == gold && profile.DustBalance == dust, "restart retry double-paid");
            Check(profile.AcknowledgeRewardPresentation(won.Id) && !profile.AcknowledgeRewardPresentation(won.Id), "V3 ack once");
            Check(profile.Gold == gold && profile.DustBalance == dust, "ack changed balances");
        }

        // Validation recomputes V3 receipts: tampered dust, gold or version is rejected without touching the file.
        foreach (var tamper in new Action<LocalProfileData>[] {
            d => d.Matches.Single(m => m.Id == won.Id).DustReward++,
            d => d.Matches.Single(m => m.Id == won.Id).Gold++,
            d => d.Matches.First(m => m.Result == "Lost").RewardVersion = 4,
            d => d.Matches.Single(m => m.Id == won.Id).RewardVersion = 2 })
        {
            string bad = Path.Combine(root, "reward-v3-bad.xml");
            var data = Read(path); tamper(data); Write(bad, data);
            byte[] bytes = File.ReadAllBytes(bad);
            Reject(() => { using var ignored = new LocalProfile(bad); }, "tampered V3 receipt");
            Check(File.ReadAllBytes(bad).SequenceEqual(bytes), "rejected V3 load changed bytes");
        }

        // A match begun on the previous build (pending V2) settles with the V2 rule; old V2 receipts reload unchanged.
        string legacy = Path.Combine(root, "reward-v2-pending.xml");
        using (var profile = new LocalProfile(legacy)) profile.Begin("mage", "priest", 9);
        var v2 = Read(legacy); v2.Pending.RewardVersion = 2; Write(legacy, v2);
        using (var profile = new LocalProfile(legacy))
        {
            var r = profile.Settle(profile.Pending.Id, "Lost", 12, false, facts);
            Check(r.RewardVersion == 2 && r.Gold == 35 && r.BonusGold == 10 && r.DustReward == 0 && profile.Gold == 35 && profile.DustBalance == 0,
                "pending V2 keeps the V2 rule");
        }
        byte[] v2Bytes = File.ReadAllBytes(legacy);
        using (var profile = new LocalProfile(legacy))
            Check(profile.History.Single().Gold == 35 && File.ReadAllBytes(legacy).SequenceEqual(v2Bytes), "V2 receipt reload unchanged");
    }
}
