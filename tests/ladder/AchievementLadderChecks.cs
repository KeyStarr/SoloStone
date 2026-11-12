internal static class AchievementLadderChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    internal static void Run(string root)
    {
        string path = Path.Combine(root, "achievement-ladder.xml");
        using var profile = new LocalProfile(path);
        int seed = 100;
        while (profile.Stars < LadderRules.LegendIndex - 1)
        {
            var pending = profile.Begin("mage", "priest", seed++);
            profile.Settle(pending.Id, "Won", 10, false);
            profile.AcknowledgeRewardPresentation(pending.Id);
        }
        LocalProfile.NpcLegendNumber = (_, __) => 1;
        LocalMatchRecord settled;
        try
        {
            var pending = profile.Begin("mage", "priest", seed);
            settled = profile.Settle(pending.Id, "Won", 10, false);
        }
        finally { LocalProfile.NpcLegendNumber = null; }
        Check(settled.TierRewards.Select(row => row.MilestoneId).SequenceEqual(
                new[] { "legend", "legend:top-100", "legend:1" })
            && settled.AchievementContext == null && settled.AchievementGrants.Count == 0
            && profile.Achievements.Unlocks.All(row => row.Id != "welcome-to-legend"
                && row.Id != "among-the-hundred" && row.Id != "there-can-be-only-one"),
            "ineligible current match forged linked ladder achievements");
        Console.WriteLine("PASS achievement ladder: current receipts remain required and direct matches cannot link");
    }
}
