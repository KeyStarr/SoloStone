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
    static void Boundary(string path, int count) => Check((int?)Marker(path)?.Element("Version") == 1
        && (int?)Marker(path)?.Element("MatchCount") == count, "calibration marker at " + count);
    static bool Same(LadderPosition a, LadderPosition b) => Xml(a) == Xml(b);
    static void Calibrated(LocalProfile p, LadderPosition old)
    {
        var expected = old.Copy(); expected.Rating = 1400; expected.PlacementGames = 0;
        Check(Same(p.Ladder, expected), "only difficulty/count change at boundary");
    }
    // A settled historical match on the current reward rule (V6 rolled, as a V13+ save holds): the plugin loads no older reward versions.
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
    // The pre-placement ("rule 1") receipt a V13+ save still carries for an old ranked match: stars folded from the results, no rating.
    internal static LocalMatchRecord Folded(LocalMatchRecord m, int starsBefore)
    {
        int after = LocalProfile.NextStars(starsBefore, m.Result);
        m.Ladder = new LocalLadderReceipt { LadderRuleVersion = 1, Outcome = m.Result, StarsBefore = starsBefore, StarsAfter = after,
            StarsGained = after - starsBefore, MultiplierBefore = 1, MultiplierAfter = 1, FloorsReached = new List<string>(),
            LegendReached = starsBefore < 150 && after == 150 };
        m.ClassXpBefore = 0; m.ClassXpAfter = 0; m.ProgressionRuleVersion = 1;
        m.UnlockedCardIds = new List<string>(); m.TierRewards = new List<LocalTierReward>();
        return m;
    }
    // Build old saves without opening the new writable loader. Existing settlement creates genuine rule-2 receipts.
    static LocalProfileData Fixture(int modern = 0, bool legacy = true, bool ties = false)
    {
        var d = (LocalProfileData)typeof(LocalProfile).GetMethod("NewProfile", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        if (legacy) {
            d.Matches.Add(Folded(Record("Won"), 0)); d.Matches.Add(Folded(Record("Lost"), 1)); d.Matches.Add(Folded(Record("Won"), 1));
            d.LadderRuleVersion = 0; d.LadderState = new LocalLadderState { StarIndex = 2, BestStarIndex = 2 };
        }
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
    internal static void Run(string root)
    {
        LegacyOnlyStartsPlacement(root);
        ShortMigratedChainRestartsOnce(root);
        LegacyEligibilityDoesNotExpire(root);
        PendingDefersCalibration(root);
        BoundaryContinuesPlacements(root);
        BoundaryTamperRejected(root);
        BoundaryFailureAtomic(root);
        Console.WriteLine("PASS rating placement: 8 cases; legacy boundary, pending ordering, replay/tamper, atomic retry");
    }
    internal static void RunCurrent(string root)
    {
        FreshPlacementUnchanged(root);
        FreshPlacementLifecycle(root);
        Console.WriteLine("PASS current rating placement: fresh K160 lifecycle, tie, settled K24, restart");
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
    static void LegacyOnlyStartsPlacement(string root)
    {
        string path = Path.Combine(root, "placement-legacy.xml"); var d = Fixture(); Write(path, d);
        var old = Position(d); string history = Xml(d.Matches), floors = Xml(d.LadderState.FloorsRewarded);
        byte[] bytes = File.ReadAllBytes(path);
        using (var p = (LocalProfile)Activator.CreateInstance(typeof(LocalProfile), BindingFlags.NonPublic | BindingFlags.Instance, null, new object[] { path, true, false }, null)) Check(Same(p.Ladder, old), "snapshot keeps legacy difficulty");
        Check(File.ReadAllBytes(path).SequenceEqual(bytes) && Marker(path) == null, "snapshot leaves bytes/marker");
        using (var p = new LocalProfile(path)) Calibrated(p, old);
        var saved = Read(path); Boundary(path, d.Matches.Count);
        Check(Xml(saved.Matches) == history && Xml(saved.LadderState.FloorsRewarded) == floors && saved.GoldBalance == d.GoldBalance
            && Xml(saved.Collection) == Xml(d.Collection) && Xml(saved.Trophies) == Xml(d.Trophies), "legacy history/rank/rewards untouched");
    }
    static void ShortMigratedChainRestartsOnce(string root)
    {
        string path = Path.Combine(root, "placement-short.xml"); var d = Fixture(8); Write(path, d);
        var old = Position(d); string history = Xml(d.Matches);
        using (var p = new LocalProfile(path)) { Calibrated(p, old); p.CommitChange(next => true); }
        Boundary(path, 11); Check(Xml(Read(path).Matches) == history, "old receipts byte-identical");
        byte[] bytes = File.ReadAllBytes(path);
        using (var p = new LocalProfile(path)) Calibrated(p, old);
        Check(File.ReadAllBytes(path).SequenceEqual(bytes), "reopen does not repeat migration");
        Check(Xml(LocalProfile.Clone(Read(path))) == Xml(Read(path)), "XML clone retains boundary");
    }
    static void LegacyEligibilityDoesNotExpire(string root)
    {
        foreach (int count in new[] { 9, 10, 35 }) foreach (bool ties in new[] { false, true }) foreach (bool legacy in new[] { false, true }) {
            string path = Path.Combine(root, $"placement-age-{count}-{ties}-{legacy}.xml"); var d = Fixture(count, legacy, ties); Write(path, d);
            var old = Position(d); string history = Xml(d.Matches);
            using (var p = new LocalProfile(path)) { if (legacy) Calibrated(p, old); else Check(Same(p.Ladder, old), "fresh origin never resets"); }
            if (legacy) Boundary(path, d.Matches.Count); else Check(Marker(path) == null, "fresh established marker absent");
            Check(Xml(Read(path).Matches) == history, "eligibility preserves history");
        }
    }
    static void PendingDefersCalibration(string root)
    {
        foreach (string mode in new[] { "settle", "cancel", "recover", "recover-unstarted" }) {
            string path = Path.Combine(root, "placement-pending-" + mode + ".xml"); var d = Fixture(9);
            d.Pending = Record(); d.Pending.Unstarted = mode.Contains("unstarted") || mode == "cancel";
            d.Pending.NpcId = "npc-probe"; d.Pending.NpcDeckId = "probe-deck"; d.Pending.NpcRating = 1730;
            string id = d.Pending.Id; Write(path, d); byte[] bytes = File.ReadAllBytes(path); var old = Position(d);
            using (var p = new LocalProfile(path)) {
                Check(Same(p.Ladder, old) && Marker(path) == null && File.ReadAllBytes(path).SequenceEqual(bytes), "pending open unchanged");
                p.CommitChange(next => true); Check(Marker(path) == null && Same(p.Ladder, old), "pending unrelated write defers");
                if (mode == "settle") p.Settle(id, "Won", 10, false);
                else if (mode == "cancel") p.CancelUnstarted(id);
                else p.RecoverInterrupted();
                Check(p.Pending == null, "pending clears");
                bool cancelled = mode == "cancel" || mode == "recover-unstarted";
                Boundary(path, d.Matches.Count + (cancelled ? 0 : 1));
                if (!cancelled) {
                    var r = p.History.Last().Ladder;
                    var step = LadderRules.Apply(old, mode == "settle" ? LadderOutcome.Win : LadderOutcome.Loss, 1730);
                    Check(r.RatingBefore == old.Rating && r.PlacementBefore == old.PlacementGames && r.OpponentRating == 1730
                        && r.RatingAfter == step.After.Rating, "pending settles from old state/stored opponent");
                    Calibrated(p, step.After);
                } else Calibrated(p, old);
                byte[] settled = File.ReadAllBytes(path); p.RecoverInterrupted();
                Check(File.ReadAllBytes(path).SequenceEqual(settled), "recovery does not duplicate penalty");
            }
        }
    }
    static void BoundaryContinuesPlacements(string root)
    {
        string path = Path.Combine(root, "placement-continue.xml"); Write(path, Fixture(8));
        using (var p = new LocalProfile(path)) {
            var before = p.Ladder; var r = Play(p, "Won", 1400).Ladder;
            Check(r.RatingBefore == 1400 && r.RatingAfter == 1480 && r.PlacementBefore == 0 && r.PlacementAfter == 1, "first post-boundary K160");
            Check(r.StarsAfter == LadderRules.Apply(before, LadderOutcome.Win, 1400).After.StarIndex, "normal star movement");
            var tieBefore = p.Ladder; Play(p, "Tied", p.Ladder.Rating); Check(Same(p.Ladder, tieBefore), "tie never advances placements");
            for (int i = 1; i < 10; i++) {
                before = p.Ladder; r = Play(p, i % 2 == 0 ? "Won" : "Lost", before.Rating).Ladder;
                var expected = LadderRules.Apply(before, i % 2 == 0 ? LadderOutcome.Win : LadderOutcome.Loss, before.Rating);
                Check(r.PlacementAfter == i + 1 && Same(p.Ladder, expected.After), "ten ordinary outcomes/normal stars");
            }
            before = p.Ladder; r = Play(p, "Won", before.Rating).Ladder;
            Check(r.RatingAfter == before.Rating + 12 && r.PlacementAfter == 10, "eleventh result K24");
            Boundary(path, 11);
        }
        using var reopened = new LocalProfile(path); Check(reopened.Ladder.PlacementGames == 10, "completed placement never restarts");
    }
    static void BoundaryTamperRejected(string root)
    {
        string goodPath = Path.Combine(root, "placement-tamper-source.xml"); Write(goodPath, Fixture(8));
        using (var p = new LocalProfile(goodPath)) Play(p, "Won", 1400);
        var good = XDocument.Load(goodPath);
        var edits = new Dictionary<string, Action<XElement>> {
            ["version"] = e => e.Element("LadderCalibration").Element("Version").Value = "2",
            ["negative"] = e => e.Element("LadderCalibration").Element("MatchCount").Value = "-1",
            ["past-end"] = e => e.Element("LadderCalibration").Element("MatchCount").Value = "9999",
            ["legacy-prefix"] = e => e.Element("LadderCalibration").Element("MatchCount").Value = "2",
            ["duplicate"] = e => e.Add(new XElement(e.Element("LadderCalibration"))),
            ["old-receipt"] = e => e.Element("Matches").Elements().ElementAt(3).Element("Ladder").Element("RatingBefore").Value = "1200",
            ["new-rating"] = e => e.Element("Matches").Elements().Last().Element("Ladder").Element("RatingBefore").Value = "1401",
            ["new-placement"] = e => e.Element("Matches").Elements().Last().Element("Ladder").Element("PlacementBefore").Value = "1",
            ["new-multiplier"] = e => { var m = e.Element("Matches").Elements().Last().Element("Ladder").Element("MultiplierBefore"); m.Value = ((int)m + 1).ToString(); },
            ["nan"] = e => e.Element("LadderState").Element("HiddenRating").Value = "NaN",
            ["infinity"] = e => e.Element("Matches").Elements().Last().Element("Ladder").Element("OpponentRating").Value = "INF",
            ["final-state"] = e => e.Element("LadderState").Element("HiddenRating").Value = "1400"
        };
        foreach (var edit in edits) {
            var bad = new XDocument(good); edit.Value(bad.Root);
            string path = Path.Combine(root, "placement-bad-" + edit.Key + ".xml"); bad.Save(path); RejectUnchanged(path, edit.Key);
        }
        string fresh = Path.Combine(root, "placement-bad-fresh.xml"); Write(fresh, Fixture(12, false));
        var f = XDocument.Load(fresh); f.Root.Add(new XElement(good.Root.Element("LadderCalibration"))); f.Save(fresh); RejectUnchanged(fresh, "fresh marker");
        string unmarked = Path.Combine(root, "placement-bad-unmarked.xml"); var corrupt = Fixture(8);
        corrupt.LadderState.HiddenRating += 1; Write(unmarked, corrupt); RejectUnchanged(unmarked, "corrupt eligible state before normalization");
        string end = Path.Combine(root, "placement-bad-boundary-end.xml"); Write(end, Fixture()); using (var p = new LocalProfile(end)) { }
        var eDoc = XDocument.Load(end); eDoc.Root.Element("LadderState").Element("HiddenRating").Value = "1401"; eDoc.Save(end); RejectUnchanged(end, "boundary end mismatch");
    }
    static void RejectUnchanged(string path, string label)
    {
        byte[] bytes = File.ReadAllBytes(path); Reject(() => { using var ignored = new LocalProfile(path); }, label);
        Check(File.ReadAllBytes(path).SequenceEqual(bytes), "tamper must not rewrite " + label);
    }
    static void BoundaryFailureAtomic(string root)
    {
        foreach (string mode in new[] { "settle", "cancel", "recover" }) {
            string path = Path.Combine(root, "placement-atomic-" + mode + ".xml"); var d = Fixture(9); d.Pending = Record();
            d.Pending.Unstarted = mode == "cancel"; Write(path, d); string history = Xml(d.Matches);
            using var p = new LocalProfile(path); byte[] bytes = File.ReadAllBytes(path); string pending = Xml(p.Pending);
            foreach (string stage in new[] { "write", "flush", "replace" }) {
                p.CommitFailure = point => { if (point == stage) throw new IOException("injected " + point); };
                Reject(() => Finish(p, mode), "boundary atomic " + mode + "/" + stage); p.CommitFailure = null;
                Check(File.ReadAllBytes(path).SequenceEqual(bytes) && Xml(p.Pending) == pending && Xml(p.History.ToList()) == history,
                    "failed boundary preserves bytes/pending/history " + mode + "/" + stage);
            }
            Finish(p, mode); Boundary(path, d.Matches.Count + (mode == "cancel" ? 0 : 1));
            Check(p.History.Count == d.Matches.Count + (mode == "cancel" ? 0 : 1), "retry adds one receipt");
            var saved = Read(path); p.CommitChange(next => true); Boundary(path, saved.Matches.Count);
            Check(Xml(LocalProfile.Clone(saved)) == Xml(saved) && Marker(path) != null, "snapshot/copy/roundtrip retain marker");
        }
    }
    static void Finish(LocalProfile p, string mode)
    {
        if (mode == "settle") p.Settle(p.Pending.Id, "Won", 10, false);
        else if (mode == "cancel") p.CancelUnstarted(p.Pending.Id);
        else p.RecoverInterrupted();
    }
}
