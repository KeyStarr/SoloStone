using System.Xml.Serialization;

internal static class RewardV5Checks
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static int Third(int amount) => (int)Math.Round(amount / 3d, MidpointRounding.AwayFromZero);
    private static LocalProfileData Read(string path)
    {
        using var stream = File.OpenRead(path);
        return (LocalProfileData)new XmlSerializer(typeof(LocalProfileData)).Deserialize(stream)!;
    }
    private static void Write(string path, LocalProfileData data)
    {
        using var stream = File.Create(path);
        new XmlSerializer(typeof(LocalProfileData)).Serialize(stream, data);
    }

    internal static void Run(string root)
    {
        Check(LocalEconomy.RollRewardVersion == 6 && LocalEconomy.IsRolled(4) && LocalEconomy.IsRolled(5)
            && LocalEconomy.IsRolled(6) && !LocalEconomy.IsRolled(3) && !LocalEconomy.IsRolled(7), "rolled versions are exactly V4/V5/V6");
        var kinds = new HashSet<int>();
        for (int seed = 0; seed < 1024; seed++)
        {
            var win = LocalEconomy.RewardV4("Won", 4, false, seed);
            kinds.Add(win.Gold > 0 ? win.Dust > 0 ? 3 : 1 : 2);
            var current = LocalEconomy.RewardV5("Won", 4, false, seed);
            Check(current.Gold == win.Gold && current.Dust == win.Dust && current.BonusGold == 0 && !current.PackReward,
                "V5 win identical to V4");
            foreach (string result in new[] { "Lost", "Tied" })
            {
                var loss = LocalEconomy.RewardV5(result, 4, false, seed);
                Check(loss.Gold == Third(win.Gold) && loss.Dust == Third(win.Dust) && loss.BonusGold == 0 && !loss.PackReward,
                    "V5 loss/draw shares win seed, kind and rounded thirds");
                var old = LocalEconomy.RewardV4(result, 4, false, seed);
                Check(old.Gold == 0 && old.Dust == 0, "V4 loss/draw remains unpaid");
            }
            var v6 = LocalEconomy.RewardV6("Won", 4, false, seed);
            Check(v6.Gold == 40 + StableSeed.Index(StableSeed.Derive(seed, 2), 31)
                && v6.Dust == 10 + StableSeed.Index(StableSeed.Derive(seed, 3), 21)
                && v6.BonusGold == 0 && !v6.PackReward, "V6 inclusive uniform gold/dust use existing seed domains");
            foreach (string result in new[] { "Lost", "Tied" }) {
                var loss = LocalEconomy.RewardV6(result, 4, false, seed);
                Check(loss.Gold == Third(v6.Gold) && loss.Dust == Third(v6.Dust), "V6 loss/draw rounded thirds");
            }
            foreach (var gate in new[] { ("Won", 3, false), ("Lost", 3, false), ("Tied", 3, false),
                ("Lost", 20, true), ("Abandoned", 20, false), ("", 20, false) })
            {
                var reward = LocalEconomy.RewardV5(gate.Item1, gate.Item2, gate.Item3, seed);
                Check(reward.Gold == 0 && reward.Dust == 0 && !reward.PackReward, "V5 gated outcome pays zero");
                reward = LocalEconomy.RewardV6(gate.Item1, gate.Item2, gate.Item3, seed);
                Check(reward.Gold == 0 && reward.Dust == 0 && !reward.PackReward, "V6 gated outcome pays zero");
            }
        }
        Check(kinds.SetEquals(new[] { 1, 2, 3 }), "all reward kinds exercised");
        foreach (var tier in GrindTiers.Selectable) Profile(root, tier);
        Console.WriteLine("PASS V6 rolls, frozen V5 rolls, losses/draws, tier rounding, atomic replay, results and mixed V4/V5/V6 receipts");
    }

    private static void Profile(string root, GrindTier tier)
    {
        string path = Path.Combine(root, "reward-v5-" + tier.Id + ".xml");
        using (var profile = new LocalProfile(path)) profile.SetGrindTier(tier.Id);
        // Begin on the previous build, then settle normally: historical receipts and balances are genuine V4.
        foreach (int version in new[] { 4, 5 })
        foreach (string result in new[] { "Won", "Lost", "Tied" })
        {
            using (var profile = new LocalProfile(path)) profile.Begin("mage", "priest", 51);
            var old = Read(path); old.Pending.RewardVersion = version; Write(path, old);
            using (var profile = new LocalProfile(path))
            {
                var match = profile.Settle(profile.Pending.Id, result, 8, false);
                var roll = version == 4 ? LocalEconomy.RewardV4(result, 8, false, LocalEconomy.RewardRollSeed(match.Id, match.Seed))
                    : LocalEconomy.RewardV5(result, 8, false, LocalEconomy.RewardRollSeed(match.Id, match.Seed));
                Check(match.RewardVersion == version && match.Gold == tier.Gold(roll.Gold) && match.DustReward == tier.Dust(roll.Dust),
                    "historical V4/V5 pending settles unchanged");
                profile.AcknowledgeRewardPresentation(match.Id);
            }
        }
        byte[] historicReceipts;
        using (var output = new MemoryStream())
        {
            new XmlSerializer(typeof(List<LocalMatchRecord>)).Serialize(output, Read(path).Matches);
            historicReceipts = output.ToArray();
        }
        foreach (var scenario in new[] { ("Won", 4, false), ("Lost", 4, false), ("Tied", 4, false),
            ("Lost", 8, true), ("Won", 3, false), ("Lost", 3, false), ("Tied", 3, false), ("Abandoned", 0, false) })
        {
            LocalMatchRecord settled;
            int goldBefore; long dustBefore;
            using (var profile = new LocalProfile(path))
            {
                var pending = profile.Begin("mage", "priest", 81);
                Check(pending.RewardVersion == 6 && pending.GrindTier == tier.Id && Read(path).Pending.RewardVersion == 6,
                    "Begin persists V6 and grind tier");
                goldBefore = profile.Gold; dustBefore = profile.DustBalance;
                if (scenario.Item1 == "Lost" && !scenario.Item3 && scenario.Item2 == 4)
                {
                    byte[] before = File.ReadAllBytes(path);
                    profile.CommitFailure = phase => { if (phase == "write") throw new IOException("V6 loss fault"); };
                    bool failed = false;
                    try { profile.Settle(pending.Id, "Lost", 4, false); }
                    catch (IOException) { failed = true; }
                    finally { profile.CommitFailure = null; }
                    Check(failed && profile.Pending.Id == pending.Id && profile.Gold == goldBefore && profile.DustBalance == dustBefore
                        && File.ReadAllBytes(path).SequenceEqual(before), "failed loss transaction preserves pending bytes/balances");
                }
                if (scenario.Item1 == "Abandoned")
                {
                    profile.RecoverInterrupted(); settled = profile.History.Last();
                }
                else settled = profile.Settle(pending.Id, scenario.Item1, scenario.Item2, scenario.Item3);
                var win = LocalEconomy.RewardV6("Won", 4, false, LocalEconomy.RewardRollSeed(pending.Id, pending.Seed));
                bool eligible = !scenario.Item3 && scenario.Item2 >= 4 && scenario.Item1 != "Abandoned";
                int gold = !eligible ? 0 : scenario.Item1 == "Won" ? win.Gold : Third(win.Gold);
                int dust = !eligible ? 0 : scenario.Item1 == "Won" ? win.Dust : Third(win.Dust);
                Check(settled.RewardVersion == 6 && settled.GrindTier == tier.Id && settled.Gold == tier.Gold(gold)
                    && settled.DustReward == tier.Dust(dust), "V6 receipt rounds thirds BEFORE tier scaling");
                Check(profile.Gold == goldBefore + settled.Gold && profile.DustBalance == dustBefore + settled.DustReward,
                    "V6 amounts applied exactly once");
                var last = profile.LastSettledMatch;
                Check(last.Id == settled.Id && last.Gold == settled.Gold && last.DustReward == settled.DustReward
                    && last.RewardVersion == 6 && last.GrindTier == tier.Id, "live hook latest receipt matches durable payout");
                last.Gold++;
                Check(profile.LastSettledMatch.Gold == settled.Gold, "live hook cannot mutate saved receipt");
                var summary = LocalRewardPresentation.Summary(settled, profile);
                var receipt = LocalResultsReceipt.FromHistory(profile.History, settled);
                Check(receipt.HasItems == eligible && receipt.Gold == settled.Gold && receipt.Dust == settled.DustReward
                    && summary.Items.Where(i => i.Kind == LocalRewardKind.Gold).Sum(i => i.Amount) == settled.Gold
                    && summary.Items.Where(i => i.Kind == LocalRewardKind.Dust).Sum(i => i.Amount) == settled.DustReward,
                    "results receipt and summary include paid losses/draws; gated results have no boxes");
                if (eligible && scenario.Item1 != "Won")
                {
                    Check(summary.Body.Contains("1/3 of a victory roll"), "loss/draw summary explains payment");
                    var panel = LocalResultPanel.Rolled(settled.Result, settled.Turns, false, settled.RewardFacts,
                        true, settled.Gold, settled.DustReward, 0, false);
                    Check(panel.Gate.Contains("1/3 of a victory roll") && panel.Total != "No gold or dust", "IMGUI paid loss/draw text");
                }
                Check(!eligible || scenario.Item1 == "Won" || !LocalCardMastery.EligibleWin(settled), "paid loss is not a mastery win");
            }
            byte[] bytes = File.ReadAllBytes(path);
            using (var reopened = new LocalProfile(path))
            {
                Check(reopened.Gold == goldBefore + settled.Gold && reopened.DustBalance == dustBefore + settled.DustReward
                    && File.ReadAllBytes(path).SequenceEqual(bytes), "mixed V4/V5/V6 replay validates without changing bytes");
                if (scenario.Item1 != "Abandoned")
                {
                    Check(reopened.PendingRewardPresentation?.Id == settled.Id, "V6 reward presentation survives restart");
                    reopened.Settle(settled.Id, scenario.Item1, scenario.Item2, scenario.Item3);
                    Check(reopened.Gold == goldBefore + settled.Gold && reopened.DustBalance == dustBefore + settled.DustReward,
                        "reopened settlement retry does not double-pay");
                    reopened.AcknowledgeRewardPresentation(settled.Id);
                }
            }
        }
        var data = Read(path);
        using (var output = new MemoryStream())
        {
            new XmlSerializer(typeof(List<LocalMatchRecord>)).Serialize(output, data.Matches.Where(m => m.RewardVersion < 6).ToList());
            Check(output.ToArray().SequenceEqual(historicReceipts), "V4/V5 receipts byte-identical beside V6");
        }
        foreach (var tamper in new Action<LocalProfileData>[] {
            d => d.Matches.First(m => m.RewardVersion == 6 && m.Result == "Lost" && !m.Conceded).Gold++,
            d => d.Matches.First(m => m.RewardVersion == 6 && m.Result == "Tied").DustReward++,
            d => d.Matches.First(m => m.RewardVersion == 6).RewardVersion = 7 })
        {
            var bad = Read(path); tamper(bad);
            string corrupt = path + ".bad"; Write(corrupt, bad); byte[] before = File.ReadAllBytes(corrupt);
            bool rejected = false;
            try { using var ignored = new LocalProfile(corrupt); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && File.ReadAllBytes(corrupt).SequenceEqual(before), "forged V6 receipt rejected without writes");
        }
    }
}
