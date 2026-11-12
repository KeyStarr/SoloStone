using System.Reflection;
using System.Xml.Linq;
using System.Xml.Serialization;

internal static class RatingPlacementChecks
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Reject(Action action, string message)
    {
        try { action(); }
        catch (Exception e) when (e is InvalidOperationException || e is ArgumentException || e is IOException) { return; }
        throw new Exception("Expected rejection: " + message);
    }
    static LocalProfileData Read(string path) => LocalProfile.ReadFile(path);
    static void Write(string path, LocalProfileData profile) { using var s = File.Create(path); new XmlSerializer(typeof(LocalProfileData)).Serialize(s, profile); }
    static string Xml<T>(T value) { using var w = new StringWriter(); new XmlSerializer(typeof(T)).Serialize(w, value); return w.ToString(); }
    static XElement Marker(string path) => XDocument.Load(path).Root.Element("LadderCalibration");
    static bool Same(LadderPosition a, LadderPosition b) => Xml(a) == Xml(b);
    // A settled historical match on the current reward rule (V6 rolled): the plugin loads no older reward versions.
    internal static LocalMatchRecord Record(string result = null, int seed = 1)
    {
        var m = new LocalMatchRecord {
            Id = Guid.NewGuid().ToString(), Deck = "historical-deck", Opponent = "priest", Seed = seed,
            StartedUtc = "2026-01-01T00:00:00.0000000Z", Result = result, Turns = result == null || result == "Abandoned" ? 0 : 10,
            RewardVersion = 6, RewardFacts = new LocalRewardFacts()
        };
        if (result == null) return m;
        var tier = GrindTiers.Find(m.GrindTier);
        var reward = LocalEconomy.RewardV6(result, m.Turns, false, LocalEconomy.RewardRollSeed(m.Id, m.Seed));
        m.Gold = tier.Gold(reward.Gold); m.BonusGold = reward.BonusGold; m.PackReward = reward.PackReward; m.DustReward = tier.Dust(reward.Dust);
        return m;
    }
    // A fresh-origin profile holding settled rule-2 history, built without opening the writable loader.
    static LocalProfileData Fixture(int modern, bool ties = false)
    {
        var d = (LocalProfileData)typeof(LocalProfile).GetMethod("NewProfile", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        string directory = Path.Combine(Path.GetTempPath(), "hs-rating-fixture-" + Guid.NewGuid().ToString("N"));
        try {
            // Supply real instance options without opening/calibrating the historical fixture being assembled.
            using var owner = new LocalProfile(Path.Combine(directory, "profile.xml"), new ProfileOptions());
            for (int i = 0; i < modern; i++) AddModern(owner, d, i == 0 ? "Abandoned" : "Won");
            if (ties) for (int i = 0; i < 4; i++) AddModern(owner, d, "Tied");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        d.GoldBalance = d.Matches.Sum(m => m.Gold);
        return d;
    }
    static void AddModern(LocalProfile owner, LocalProfileData d, string result)
    {
        var m = Record(result);
        var apply = typeof(LocalProfile).GetMethod("ApplyMatchReceipt", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingMethodException(nameof(LocalProfile), "ApplyMatchReceipt");
        apply.Invoke(owner, new object[] { d, m, null, m.DustReward });
        d.Matches.Add(m);
    }
    static LadderPosition Position(LocalProfileData d)
        => (LadderPosition)typeof(LocalProfile).GetMethod("LadderOf", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { d });
    static LocalMatchRecord Play(LocalProfile p, string result, double? rating = null)
    {
        var m = p.Begin("mage", "priest", 1);
        var settled = p.Settle(m.Id, result, 10, false, null, rating);
        p.AcknowledgeRewardPresentation(m.Id);
        return settled;
    }
    internal static void RunCurrent(string root)
    {
        FreshPlacementUnchanged(root);
        FreshPlacementLifecycle(root);
        FreshHistoryNeverCalibrates(root);
        Console.WriteLine("PASS current rating placement: fresh K160 lifecycle, tie, settled K24, restart, long history never calibrates");
    }
    static void FreshPlacementUnchanged(string root)
    {
        string path = Path.Combine(root, "placement-fresh.xml");
        using var p = new LocalProfile(path);
        Check(p.Ladder.Rating == 1400 && p.Ladder.PlacementGames == 0, "fresh 1400/0");
        var r = Play(p, "Won", 1400).Ladder;
        Check(r.RatingAfter == 1480 && r.PlacementBefore == 0 && r.PlacementAfter == 1 && r.StarsGained == 6, "fresh K160/normal stars");
        Check(Marker(path) == null, "fresh marker absent");
    }
    static void FreshPlacementLifecycle(string root)
    {
        string path = Path.Combine(root, "placement-fresh-lifecycle.xml");
        using (var p = new LocalProfile(path)) {
            var tied = p.Ladder; Play(p, "Tied", tied.Rating);
            Check(Same(p.Ladder, tied), "fresh tie never advances placements");
            for (int i = 0; i < LadderRules.PlacementLength; i++) {
                var before = p.Ladder; string result = i % 2 == 0 ? "Won" : "Lost";
                var receipt = Play(p, result, before.Rating).Ladder;
                var expected = LadderRules.Apply(before, LadderRules.Outcome(result), before.Rating);
                Check(receipt.PlacementAfter == i + 1 && Same(p.Ladder, expected.After), "fresh placement result " + i);
            }
            var settledBefore = p.Ladder;
            var settled = Play(p, "Won", settledBefore.Rating).Ladder;
            Check(settled.PlacementBefore == LadderRules.PlacementLength && settled.PlacementAfter == LadderRules.PlacementLength
                && settled.RatingAfter == settledBefore.Rating + LadderRules.SettledK / 2, "first settled result uses K24");
        }
        using var reopened = new LocalProfile(path);
        Check(reopened.Ladder.PlacementGames == LadderRules.PlacementLength, "fresh completed placement survives restart");
    }
    static void FreshHistoryNeverCalibrates(string root)
    {
        // 9, 10 and 35 recorded games: one below the placement length (10), exactly at it, and well past it. A fresh-origin
        // profile never gets the one-time legacy calibration whatever its history length, so none of them may reset.
        foreach (int count in new[] { 9, 10, 35 }) foreach (bool ties in new[] { false, true }) {
            string path = Path.Combine(root, $"placement-age-{count}-{ties}.xml"); var d = Fixture(count, ties); Write(path, d);
            var old = Position(d); string history = Xml(d.Matches);
            using (var p = new LocalProfile(path)) Check(Same(p.Ladder, old), "fresh origin never resets");
            Check(Marker(path) == null, "fresh established marker absent");
            Check(Xml(Read(path).Matches) == history, "history preserved");
        }
        string bad = Path.Combine(root, "placement-bad-fresh.xml"); Write(bad, Fixture(12));
        var f = XDocument.Load(bad); f.Root.Add(new XElement("LadderCalibration", new XElement("Version", 1), new XElement("MatchCount", 5))); f.Save(bad);
        byte[] bytes = File.ReadAllBytes(bad);
        Reject(() => { using var ignored = new LocalProfile(bad); }, "calibration marker on a fresh-origin profile");
        Check(File.ReadAllBytes(bad).SequenceEqual(bytes), "rejection must not rewrite the save");
    }
}
