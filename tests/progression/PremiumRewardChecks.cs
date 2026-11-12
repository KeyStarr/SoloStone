using System.Xml.Serialization;

internal static class PremiumRewardChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception("Premium rewards: " + message); }
    private static void Reject(Action action)
    {
        try { action(); } catch (Exception error) when (error is InvalidOperationException || error is IOException) { return; }
        throw new Exception("Expected premium reward refusal");
    }
    internal static void Run(string root)
    {
        string path = Path.Combine(root, "premium-rewards.xml"), matchId, cardId;
        int premium, copies;
        using (var profile = new LocalProfile(path))
        {
            Reject(() => profile.PreparePremiumUnlockForTest("missing"));
            var match = profile.Begin("mage", "priest", 123);
            Reject(() => profile.PreparePremiumUnlockForTest("stale"));
            byte[] before = File.ReadAllBytes(path);
            var beforePremium = profile.FeatsSnapshot().PremiumCollection;
            int beforeXp = profile.ClassXp["HERO_08"];
            foreach (string stage in new[] { "write", "flush", "replace" })
            {
                profile.CommitFailure = point => { if (point == stage) throw new IOException("prepare failure"); };
                Reject(() => profile.PreparePremiumUnlockForTest(match.Id));
                Check(before.SequenceEqual(File.ReadAllBytes(path)) && profile.ClassXp["HERO_08"] == beforeXp,
                    "failed preparation changed saved/in-memory progress");
            }
            profile.CommitFailure = null;
            var planned = profile.PreparePremiumUnlockForTest(match.Id);
            cardId = planned.CardId; premium = planned.Premium;
            copies = beforePremium.SingleOrDefault(row => row.Id == cardId && row.Premium == premium)?.Count ?? 0;
            Check(profile.Pending.Id == match.Id && profile.OwnedCount(cardId, premium) == copies
                && profile.History.Count == 0, "preparation altered match/history or granted cards");
            var settled = profile.Settle(match.Id, "Won", 4, false);
            matchId = settled.Id;
            Check(settled.ClassRewardGrants.Any(row => row.CardId == cardId && row.Premium == premium && row.Copies == planned.Copies), "real settlement missed prepared reward");
            var receipt = LocalResultsReceipt.FromHistory(profile.History, profile.PendingRewardPresentation);
            Check(receipt.PremiumUnlocks.Any(row => row.Id == cardId && row.Premium == premium && row.Count == planned.Copies), "receipt omitted premium card");
            copies += planned.Copies;
            Check(profile.OwnedCount(cardId, premium) == copies, "receipt differs from collection");
        }
        byte[] settledBytes = File.ReadAllBytes(path);
        using (var profile = new LocalProfile(path))
        {
            Check(profile.PendingRewardPresentation?.Id == matchId && LocalResultsReceipt.FromHistory(profile.History,
                profile.PendingRewardPresentation).PremiumUnlocks.Any(row => row.Id == cardId && row.Premium == premium), "restart lost unseen cards");
            Check(profile.OwnedCount(cardId, premium) == copies && settledBytes.SequenceEqual(File.ReadAllBytes(path)), "replay regranted cards");
            foreach (string stage in new[] { "write", "flush", "replace" })
            {
                profile.CommitFailure = point => { if (point == stage) throw new IOException("ack failure"); };
                Reject(() => profile.AcknowledgeRewardPresentation(matchId));
                Check(profile.PendingRewardPresentation?.Id == matchId && settledBytes.SequenceEqual(File.ReadAllBytes(path)), "failed ack dropped unseen cards");
            }
            profile.CommitFailure = null;
            profile.AcknowledgeRewardPresentation(matchId);
        }
        using (var profile = new LocalProfile(path))
            Check(profile.PendingRewardPresentation == null && profile.OwnedCount(cardId, premium) == copies
                && profile.History.Single().ClassRewardGrants.Any(row => row.CardId == cardId), "ack lost cards/history or replayed presentation");
        using (var stream = File.OpenRead(path))
        {
            var data = (LocalProfileData)new XmlSerializer(typeof(LocalProfileData)).Deserialize(stream);
            data.ClassXp.Single(row => row.Hero == "HERO_08").Xp = int.MaxValue;
            using var output = File.Create(Path.Combine(root, "premium-rewards-cap.xml"));
            new XmlSerializer(typeof(LocalProfileData)).Serialize(output, data);
        }
        using (var profile = new LocalProfile(Path.Combine(root, "premium-rewards-cap.xml")))
        {
            var match = profile.Begin("mage", "priest", 124);
            Reject(() => profile.PreparePremiumUnlockForTest(match.Id));
        }
        using (var profile = new LocalProfile(Path.Combine(root, "premium-rewards-casual.xml")))
        {
            var match = profile.Begin("mage", "priest", 125, null, null, null, CasualRules.Mode);
            Reject(() => profile.PreparePremiumUnlockForTest(match.Id));
        }
        using (var profile = new LocalProfile(Path.Combine(root, "premium-rewards-scenario.xml")))
        {
            profile.ArmDebugScenario();
            var match = profile.Begin("mage", "priest", 126);
            Reject(() => profile.PreparePremiumUnlockForTest(match.Id));
        }
        Console.WriteLine("PASS premium rewards: live preparation, real receipt/collection, unseen restart, atomic ack failures, exhausted schedule");
    }
}
