using System.Xml.Serialization;

// Ladder ledger: atomic settle with receipt, crash before commit, idempotent replay, abandonment exactly once,
// receipt-chain tamper rejection, medal projection from the ledger.
internal static class LadderLedgerChecks
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Reject(Action action, string message)
    {
        try { action(); }
        catch (Exception e) when (e is InvalidOperationException || e is ArgumentException || e is IOException) { return; }
        throw new Exception("Expected rejection: " + message);
    }
    static LocalProfileData Read(string path) { using var s = File.OpenRead(path); return (LocalProfileData)new XmlSerializer(typeof(LocalProfileData)).Deserialize(s); }
    static void Write(string path, LocalProfileData p) { using var s = File.Create(path); new XmlSerializer(typeof(LocalProfileData)).Serialize(s, p); }
    static LocalMatchRecord Play(LocalProfile p, string result, int seed)
    {
        var match = p.Begin("mage", "priest", seed);
        var settled = p.Settle(match.Id, result, 10, false);
        p.AcknowledgeRewardPresentation(match.Id);
        return settled;
    }
    static bool SameState(LadderPosition a, LadderPosition b) => a.StarIndex == b.StarIndex && a.BestStarIndex == b.BestStarIndex
        && a.Multiplier == b.Multiplier && a.StreakWins == b.StreakWins && a.PlacementGames == b.PlacementGames && a.Rating == b.Rating
        && a.LegendReached == b.LegendReached;
    static bool Matches(LocalLadderReceipt r, LadderPosition after) => r.StarsAfter == after.StarIndex && r.BestAfter == after.BestStarIndex
        && r.MultiplierAfter == after.Multiplier && r.StreakAfter == after.StreakWins && r.PlacementAfter == after.PlacementGames
        && r.RatingAfter == after.Rating;

    internal static void RunCurrent(string root)
    {
        var rows = Enumerable.Range(1, 51).Select(level => new LadderRankRow { StarLevel = level, Stars = level == 51 ? 0 : 3, Legend = level == 51 }).ToArray();

        // Fresh profile: settle writes ladder state and receipt in one commit, persisted across restart.
        string path = Path.Combine(root, "ladder-ledger.xml");
        LocalLadderReceipt first;
        LocalProfileData firstProfile;
        using (var p = new LocalProfile(path))
        {
            var fresh = LadderRules.Fresh();
            Check(SameState(p.Ladder, fresh) && p.Stars == 0, "fresh profile ladder");
            var medalBefore = LadderRules.Medal(p.Ladder, 1, rows);
            Check(medalBefore.StarLevel == 1 && medalBefore.StarsPerWin == fresh.Multiplier, "medal before first match");
            var won = Play(p, "Won", 1);
            first = won.Ladder;
            Check(first != null && first.LadderRuleVersion == LadderRules.RuleVersion && first.Outcome == "Win" && first.StarsBefore == 0
                && first.StarsAfter == fresh.Multiplier && first.StarsGained == fresh.Multiplier && first.MultiplierBefore == fresh.Multiplier
                && first.RatingBefore == LadderRules.StartRating && first.RatingAfter == LadderRules.StartRating + LadderRules.PlacementK / 2
                && first.PlacementAfter == 1 && first.StreakAfter == 1 && Matches(first, p.Ladder), "first settlement receipt");
            var medalAfter = LadderRules.Medal(p.Ladder, 1, rows);
            Check(medalAfter.StarLevel == p.Stars / 3 + 1 && medalAfter.Stars == p.Stars % 3 && medalAfter.StarsPerWin == first.MultiplierAfter
                && medalAfter.Streak == 1, "medal after match matches the receipt");
            Check(p.LadderArrivedByWin == (first.StarsGained > 0), "medal form follows the last star move");
            var saved = Read(path);
            firstProfile = LocalProfile.Clone(saved);
            Check(saved.LadderRuleVersion == LadderRules.RuleVersion && saved.LadderState.HiddenRating == first.RatingAfter
                && saved.Matches.Single().Ladder.StarsAfter == first.StarsAfter, "ladder state and receipt saved together");
        }
        using (var p = new LocalProfile(path))
        {
            Check(Matches(first, p.Ladder) && p.LadderReceipt(p.History.Single().Id).StarsAfter == first.StarsAfter, "restart keeps ladder");
            // Idempotent replay returns the saved receipt without changing state; a conflicting one is rejected.
            byte[] bytes = File.ReadAllBytes(path);
            var replay = p.Settle(p.History.Single().Id, "Won", 10, false);
            Check(replay.Ladder.StarsAfter == first.StarsAfter && File.ReadAllBytes(path).SequenceEqual(bytes), "idempotent replay");
            Reject(() => p.Settle(p.History.Single().Id, "Lost", 10, false), "conflicting replay");
            Reject(() => p.Settle(p.History.Single().Id, "Won", 10, false, null, 900), "replay with a different opponent rating");
            Check(p.Settle(p.History.Single().Id, "Won", 10, false, null, first.OpponentRating).Ladder.StarsAfter == first.StarsAfter,
                "replay with the same opponent rating");
            Check(File.ReadAllBytes(path).SequenceEqual(bytes) && Matches(first, p.Ladder), "conflicting replay left state");

            // Crash before or during the settlement commit: nothing changes; the retry applies exactly once.
            var pending = p.Begin("mage", "priest", 2);
            byte[] beforeSettle = File.ReadAllBytes(path);
            var ladderBefore = p.Ladder;
            foreach (string stage in new[] { "write", "flush", "replace" })
            {
                p.CommitFailure = point => { if (point == stage) throw new IOException("injected " + point); };
                Reject(() => p.Settle(pending.Id, "Won", 10, false), "settle failure " + stage);
                p.CommitFailure = null;
                Check(File.ReadAllBytes(path).SequenceEqual(beforeSettle) && SameState(p.Ladder, ladderBefore), "atomic settle " + stage);
            }
            var second = p.Settle(pending.Id, "Won", 10, false).Ladder;
            p.AcknowledgeRewardPresentation(pending.Id);
            Check(second.StarsBefore == first.StarsAfter && second.PlacementAfter == 2 && Matches(second, p.Ladder), "retry settles once");

            // Ties: nothing changes, streak kept.
            var tie = Play(p, "Tied", 3).Ladder;
            Check(tie.Outcome == "Tie" && tie.StarsGained == 0 && tie.StreakAfter == 2 && tie.RatingAfter == tie.RatingBefore
                && tie.PlacementAfter == tie.PlacementBefore, "tie receipt neutral");
            var third = Play(p, "Won", 4).Ladder;
            Check(third.StreakBonus && third.StarsGained == 2 * third.MultiplierBefore || third.StoppedAtDiamondFive, "third win pays the streak bonus");
        }

        // Abandonment: a crash mid-match is a loss applied by recovery, exactly once across restarts.
        LadderPosition beforeAbandon;
        using (var p = new LocalProfile(path)) { beforeAbandon = p.Ladder; p.Begin("mage", "priest", 5); }
        using (var p = new LocalProfile(path))
        {
            byte[] crashed = File.ReadAllBytes(path);
            foreach (string stage in new[] { "write", "flush", "replace" })
            {
                p.CommitFailure = point => { if (point == stage) throw new IOException("injected " + point); };
                Reject(() => p.RecoverInterrupted(), "recovery failure " + stage);
                p.CommitFailure = null;
                Check(File.ReadAllBytes(path).SequenceEqual(crashed) && p.Pending != null && SameState(p.Ladder, beforeAbandon), "atomic recovery " + stage);
            }
        }
        LocalLadderReceipt abandoned;
        using (var p = new LocalProfile(path))
        {
            p.RecoverInterrupted();
            abandoned = p.History.Last().Ladder;
            Check(p.History.Last().Result == "Abandoned" && abandoned.Outcome == "Loss" && abandoned.StreakAfter == 0
                && abandoned.StarsAfter == Math.Max(LadderRules.FloorBelow(beforeAbandon.StarIndex), beforeAbandon.StarIndex - 1)
                && abandoned.RatingAfter < abandoned.RatingBefore && Matches(abandoned, p.Ladder), "abandon applies a loss");
            Check(abandoned.StarsGained < 0 && !p.LadderArrivedByWin, "after a star loss the medal shows the 0/3 form");
        }
        // History/LadderReceipt copies carry every receipt field (a field-by-field copy must not drop new fields).
        using (var p = new LocalProfile(path))
        {
            var receiptXml = new XmlSerializer(typeof(LocalLadderReceipt));
            string Xml(LocalLadderReceipt r) { using var w = new StringWriter(); receiptXml.Serialize(w, r); return w.ToString(); }
            var saved = Read(path).Matches;
            Check(p.History.Select(m => m.Ladder == null ? "" : Xml(m.Ladder)).SequenceEqual(saved.Select(m => m.Ladder == null ? "" : Xml(m.Ladder)))
                && Xml(p.LadderReceipt(saved.Last().Id)) == Xml(saved.Last().Ladder), "receipt copies are complete");
        }
        byte[] recovered = File.ReadAllBytes(path);
        using (var p = new LocalProfile(path))
        {
            p.RecoverInterrupted();
            Check(File.ReadAllBytes(path).SequenceEqual(recovered) && Matches(abandoned, p.Ladder)
                && p.History.Count(m => m.Result == "Abandoned") == 1, "second restart changes nothing");
        }

        // Tampering: receipts must chain, follow the rules and end at the stored state.
        var good = Read(path);
        var tampers = new Dictionary<string, Action<LocalProfileData>> {
            ["receipt stars"] = d => d.Matches[1].Ladder.StarsAfter++,
            ["receipt chain"] = d => d.Matches[2].Ladder.StreakBefore = 0,
            ["receipt outcome"] = d => d.Matches[0].Ladder.Outcome = "Loss",
            ["missing receipt"] = d => d.Matches[1].Ladder = null,
            ["state multiplier"] = d => d.LadderState.Multiplier = 1 + d.LadderState.Multiplier % 3,
            ["state stars"] = d => d.LadderState.StarIndex = d.LadderState.BestStarIndex = d.LadderState.StarIndex + 1,
            ["state rating"] = d => d.LadderState.HiddenRating = null,
            ["rule version"] = d => d.LadderRuleVersion = LadderRules.RuleVersion + 1,
            ["reservation with receipts"] = d => d.LadderRuleVersion = 0,
            ["pending receipt"] = d => d.Pending = new LocalMatchRecord { Id = Guid.NewGuid().ToString(), Deck = "mage", Opponent = "priest",
                StartedUtc = DateTime.UtcNow.ToString("o"), RewardVersion = 2, RewardFacts = new LocalRewardFacts(), Ladder = d.Matches[0].Ladder },
        };
        foreach (var tamper in tampers)
        {
            var bad = Read(path); tamper.Value(bad);
            string badPath = Path.Combine(root, "ladder-tamper-" + tamper.Key.Replace(' ', '-') + ".xml"); Write(badPath, bad);
            byte[] badBytes = File.ReadAllBytes(badPath);
            Reject(() => { using var ignored = new LocalProfile(badPath); }, "tamper " + tamper.Key);
            Check(File.ReadAllBytes(badPath).SequenceEqual(badBytes), "tamper rewrote " + tamper.Key);
        }

        // Forged first receipt: a valid Legend->Legend win replayed from nowhere must not load.
        var forged = LocalProfile.Clone(firstProfile);
        forged.Matches = forged.Matches.Take(1).ToList(); forged.PendingRewardPresentationId = null;
        var fr = forged.Matches[0].Ladder;
        double rating = fr.RatingBefore.Value;
        fr.StarsBefore = fr.StarsAfter = fr.BestBefore = fr.BestAfter = 150; fr.StarsGained = 0; fr.MultiplierBefore = fr.MultiplierAfter = 1;
        fr.StreakBefore = 0; fr.StreakAfter = 1; fr.PlacementBefore = fr.PlacementAfter = LadderRules.PlacementLength;
        fr.RatingAfter = rating + LadderRules.SettledK / 2; fr.OpponentRating = rating; fr.StreakBonus = fr.StoppedAtDiamondFive = fr.LegendReached = false;
        fr.FloorsReached = new List<string>();
        forged.LadderState.StarIndex = forged.LadderState.BestStarIndex = 150; forged.LadderState.Multiplier = 1; forged.LadderState.StreakWins = 1;
        forged.LadderState.PlacementGames = LadderRules.PlacementLength; forged.LadderState.HiddenRating = fr.RatingAfter; forged.LadderState.LegendReached = true;
        string forgedPath = Path.Combine(root, "ladder-forged-first.xml"); Write(forgedPath, forged);
        Reject(() => { using var ignored = new LocalProfile(forgedPath); }, "forged first receipt");
        forged.Matches[0].Ladder = LocalProfile.Clone(firstProfile.Matches[0].Ladder); forged.LadderState = LocalProfile.Clone(firstProfile.LadderState);
        var honest = firstProfile;
        forged.LadderState.StarIndex = honest.Matches[0].Ladder.StarsAfter; forged.LadderState.BestStarIndex = honest.Matches[0].Ladder.BestAfter;
        forged.LadderState.Multiplier = honest.Matches[0].Ladder.MultiplierAfter; forged.LadderState.StreakWins = honest.Matches[0].Ladder.StreakAfter;
        forged.LadderState.PlacementGames = honest.Matches[0].Ladder.PlacementAfter; forged.LadderState.HiddenRating = honest.Matches[0].Ladder.RatingAfter;
        forged.LadderState.LegendReached = false; Write(forgedPath, forged);
        using (var p = new LocalProfile(forgedPath)) Check(p.Stars == first.StarsAfter, "control: the honest one-match profile loads");

        Console.WriteLine("PASS current ladder ledger: atomic settle/recovery, idempotent replay, abandon once, receipt-chain/tamper rejection, medal");
    }
}
