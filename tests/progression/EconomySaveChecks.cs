using System.Xml.Serialization;

internal static class EconomySaveChecks
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static LocalProfileData Read(string path)
    {
        using var input = File.OpenRead(path);
        return (LocalProfileData)new XmlSerializer(typeof(LocalProfileData)).Deserialize(input)!;
    }
    private static string Receipts(IEnumerable<LocalMatchRecord> matches)
    {
        using var output = new StringWriter();
        new XmlSerializer(typeof(List<LocalMatchRecord>)).Serialize(output, matches.ToList());
        return output.ToString();
    }

    internal static void Run(string copyPath)
    {
        var before = Read(copyPath);
        string history = Receipts(before.Matches);
        Check(before.GrindTier == GrindTiers.Relaxed2xId && before.Pending == null, "expected idle old Dad-gamer copy");
        LocalMatchRecord settled;
        // Production open path validates every historical payout before upgrading current pace.
        using (var profile = new LocalProfile(copyPath))
        {
            Check(profile.GrindTier == GrindTiers.Relaxed6x && Read(copyPath).GrindTier == GrindTiers.Relaxed6xId,
                "copy current pace upgrades durably");
            Check(Receipts(profile.History) == history, "historical receipt bytes preserved");
            Check(profile.Gold == before.GoldBalance && profile.DustBalance == before.DustBalance,
                "upgrade preserves currency balances");
            if (profile.PendingRewardPresentation != null)
                profile.AcknowledgeRewardPresentation(profile.PendingRewardPresentation.Id);
            var pending = profile.Begin("player-core-mage", "priest", 20261101);
            int gold = profile.Gold; long dust = profile.DustBalance;
            settled = profile.Settle(pending.Id, "Won", 8, false);
            var roll = LocalEconomy.RewardV6("Won", 8, false, LocalEconomy.RewardRollSeed(pending.Id, pending.Seed));
            Check(settled.RewardVersion == 6 && settled.GrindTier == GrindTiers.Relaxed6xId
                && settled.Gold == 6 * roll.Gold && settled.DustReward == 6 * roll.Dust
                && settled.Gold > 0 && settled.DustReward > 0, "V6 both-currency win under new tier");
            Check(profile.Gold == gold + settled.Gold && profile.DustBalance == dust + settled.DustReward,
                "win paid once without unrelated grants");
            profile.AcknowledgeRewardPresentation(settled.Id);
        }
        byte[] after = File.ReadAllBytes(copyPath);
        using (var reopened = new LocalProfile(copyPath))
        {
            Check(reopened.GrindTier == GrindTiers.Relaxed6x && reopened.History.Last().Id == settled.Id
                && reopened.History.Count == before.Matches.Count + 1, "new receipt validates after restart");
            Check(Receipts(reopened.History.Take(before.Matches.Count)) == history, "old history unchanged after win/restart");
            Check(File.ReadAllBytes(copyPath).SequenceEqual(after), "restart is idempotent");
        }
        Console.WriteLine($"SAVE_COPY_PASS {copyPath} oldMatches={before.Matches.Count} tier={settled.GrindTier} rewardVersion={settled.RewardVersion} gold={settled.Gold} dust={settled.DustReward} matchId={settled.Id}");
    }
}
