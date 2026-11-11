using System.Globalization;
using System.Xml.Serialization;

internal static class MatchTimingChecks
{
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    internal static void Run(string root)
    {
        string path = Path.Combine(root, "match-timing", "profile.xml");
        string settled;
        using (var profile = new LocalProfile(path))
        {
            var pending = profile.Begin("player-core-mage", "priest", 51);
            Check(pending.SettledUtc == null && profile.Pending.SettledUtc == null
                && !File.ReadAllText(path).Contains("SettledUtc"), "pending has no settlement timestamp");
            var result = profile.Settle(pending.Id, "Won", 8, false);
            settled = result.SettledUtc;
            Check(DateTime.TryParseExact(settled, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)
                && date.Kind == DateTimeKind.Utc, "settlement stamps UTC round-trip date");
            Check(profile.Settle(pending.Id, "Won", 8, false).SettledUtc == settled, "retry preserves timestamp");
            profile.AcknowledgeRewardPresentation(pending.Id);
        }
        using (var profile = new LocalProfile(path))
        {
            Check(profile.History.Single().SettledUtc == settled, "reopen preserves timestamp");
            var pending = profile.Begin("player-core-mage", "priest", 52);
            Check(profile.Settle(pending.Id, "Lost", 3, true).SettledUtc != null, "concede stamps settlement");
            profile.AcknowledgeRewardPresentation(pending.Id);
            profile.Begin("player-core-mage", "priest", 53);
        }
        using (var profile = new LocalProfile(path))
        {
            profile.RecoverInterrupted();
            Check(profile.Pending == null && profile.History.Last().Result == "Abandoned"
                && profile.History.Last().SettledUtc != null, "recovery stamps abandonment");
        }
        var xml = new XmlSerializer(typeof(LocalProfileData));
        LocalProfileData data;
        using (var input = File.OpenRead(path)) data = (LocalProfileData)xml.Deserialize(input)!;
        void Save() { using var output = File.Create(path); xml.Serialize(output, data); }
        foreach (var match in data.Matches) match.SettledUtc = null;
        Save();
        using (var profile = new LocalProfile(path))
            Check(profile.History.All(match => match.SettledUtc == null), "legacy receipts without timestamp load");
        foreach (string invalid in new[] { "bad-date", "2026-10-31T12:00:00+03:00" })
        {
            data.Matches[0].SettledUtc = invalid; Save();
            bool rejected = false;
            try { using var profile = new LocalProfile(path); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "invalid settlement timestamp rejected: " + invalid);
        }
        data.Matches[0].SettledUtc = null; Save();
        using (var profile = new LocalProfile(path)) profile.Begin("player-core-mage", "priest", 54);
        using (var input = File.OpenRead(path)) data = (LocalProfileData)xml.Deserialize(input)!;
        data.Pending.SettledUtc = settled; Save();
        bool pendingRejected = false;
        try { using var profile = new LocalProfile(path); } catch (InvalidOperationException) { pendingRejected = true; }
        Check(pendingRejected, "pending settlement timestamp rejected");
        Console.WriteLine("Match timing checks passed");
    }
}
