using System.Xml.Serialization;

internal static class RewardV7Checks
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    internal static void Profile(string path, GrindTier tier, List<LocalMatchRecord> historical)
    {
        foreach (var (active, milliseconds, progress) in new[] {
            (0, 540000, 0), (1, 540000, 0), (2, 0, 0), (2, 540000, 135000),
            (3, 180000, 180000), (3, 540000, 270000), (4, 540000, 405000),
            (5, 539999, 539999), (5, 540000, 540000) })
        {
            var full = LocalEconomy.RewardV6("Lost", 10, false, 81);
            var facts = new LocalRewardFacts { ActiveTurns = active, PlayMilliseconds = milliseconds };
            foreach (bool conceded in new[] { false, true })
            {
                var reward = LocalEconomy.RewardV7("Lost", 10, conceded, facts, 81, tier);
                Check(reward.Gold == tier.Gold(full.Gold) * progress / 540000
                    && reward.Dust == tier.Dust(full.Dust) * progress / 540000
                    && reward.BonusGold == 0 && !reward.PackReward, "V7 pays the lower share after pace scaling");
            }
        }

        var participation = new LocalRewardFacts { ActiveTurns = 3, PlayMilliseconds = 180000 };
        LocalMatchRecord paid;
        int gold;
        long dust;
        using (var profile = new LocalProfile(path))
        {
            var pending = profile.Begin("mage", "priest", 82);
            Check(pending.RewardVersion == 7 && pending.GrindTier == tier.Id, "V7 and pace stamped at launch");
            gold = profile.Gold;
            dust = profile.DustBalance;
            byte[] before = File.ReadAllBytes(path);
            profile.CommitFailure = phase => { if (phase == "write") throw new IOException("V7 loss write fault"); };
            try
            {
                profile.Settle(pending.Id, "Lost", 7, true, participation);
                throw new Exception("V7 settlement should fail for injected write fault");
            }
            catch (IOException) { }
            finally { profile.CommitFailure = null; }
            Check(profile.Pending.Id == pending.Id && profile.Gold == gold && profile.DustBalance == dust
                && File.ReadAllBytes(path).SequenceEqual(before), "V7 save failure preserves the pending match and balances");

            paid = profile.Settle(pending.Id, "Lost", 7, true, participation);
            var roll = LocalEconomy.RewardV6("Lost", 7, false, LocalEconomy.RewardRollSeed(pending.Id, pending.Seed));
            Check(paid.Conceded && paid.Gold == tier.Gold(roll.Gold) / 3 && paid.DustReward == tier.Dust(roll.Dust) / 3
                && profile.Gold == gold + paid.Gold && profile.DustBalance == dust + paid.DustReward, "paid concede updates both balances once");
            var summary = LocalRewardPresentation.Summary(paid, profile);
            Check(summary.Body.Contains("33% of a full loss reward") && summary.Body.Contains("9 minutes")
                && LocalResultsReceipt.FromHistory(profile.History, paid).HasItems, "native summary presents the concede payout");
        }
        byte[] saved = File.ReadAllBytes(path);
        using (var profile = new LocalProfile(path))
        {
            Check(profile.PendingRewardPresentation.Id == paid.Id
                && profile.PendingRewardPresentation.RewardFacts.ActiveTurns == 3
                && profile.PendingRewardPresentation.RewardFacts.PlayMilliseconds == 180000,
                "V7 receipt and participation survive reopening");
            profile.Settle(paid.Id, "Lost", 7, true, participation);
            Check(profile.Gold == gold + paid.Gold && profile.DustBalance == dust + paid.DustReward
                && File.ReadAllBytes(path).SequenceEqual(saved), "V7 retry returns the saved payout without rewriting the ledger");
            Check(MatchXml(profile.History.Take(historical.Count).ToList()) == MatchXml(historical),
                "prior receipts are byte-identical beside V7");
        }
        Console.WriteLine("PASS V7 participation curve, paid concede, atomic retry, mixed reward history and reload: " + tier.Id);
    }

    private static string MatchXml(List<LocalMatchRecord> matches)
    {
        using var output = new StringWriter();
        new XmlSerializer(typeof(List<LocalMatchRecord>)).Serialize(output, matches);
        return output.ToString();
    }
}
