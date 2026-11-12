using System.Xml.Serialization;

// Live trap: a native launch that threw after Begin left a Pending (Turns 0). Every later start returned early, and the
// next launch settled it as Abandoned: a ladder loss for a match that never began. BeginLaunch clears it atomically.
internal static class StartTrapChecks
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static LocalProfileData Read(string path) { using var s = File.OpenRead(path); return (LocalProfileData)new XmlSerializer(typeof(LocalProfileData)).Deserialize(s); }
    static Exception Failure(Action action)
    {
        try { action(); return null; } catch (Exception error) { return error; }
    }

    static bool NoPenalty(LocalProfile p, int history, int stars, int gold, LadderPosition ladder, Dictionary<string, int> xp) =>
        p.History.Count == history && p.Stars == stars && p.Gold == gold && p.Ladder.StarIndex == ladder.StarIndex
        && p.Ladder.Rating == ladder.Rating && p.Ladder.StreakWins == ladder.StreakWins && p.Ladder.PlacementGames == ladder.PlacementGames
        && p.ClassXp.All(row => xp[row.Key] == row.Value);

    internal static void Run(string root)
    {
        string path = Path.Combine(root, "start-trap.xml");
        using (var p = new LocalProfile(path))
        {
            var won = p.Begin("mage", "priest", 1);
            p.Settle(won.Id, "Won", 10, false);
            p.AcknowledgeRewardPresentation(won.Id);
        }
        using (var p = new LocalProfile(path))
        {
            var ladder = p.Ladder;
            int history = p.History.Count, stars = p.Stars, gold = p.Gold;
            var xp = p.ClassXp.ToDictionary(row => row.Key, row => row.Value);
            string seen = null;
            var failure = Failure(() => p.BeginLaunch("mage", "priest", 2, record => {
                seen = record.Id;
                Check(p.Pending?.Id == record.Id && Read(path).Pending?.Id == record.Id, "launch must run after Pending is durable");
                throw new InvalidOperationException("Unknown local card.");
            }));
            Check(failure is InvalidOperationException && failure.Message == "Unknown local card." && seen != null,
                "launch failure must surface its own error");
            Check(p.Pending == null && Read(path).Pending == null, "unstarted Pending must be cleared durably");
            Check(p.History.Count == history && p.Stars == stars && p.Gold == gold && p.Ladder.StarIndex == ladder.StarIndex
                && p.Ladder.Rating == ladder.Rating && p.Ladder.StreakWins == ladder.StreakWins && p.Ladder.PlacementGames == ladder.PlacementGames
                && p.ClassXp.All(row => xp[row.Key] == row.Value), "launch failure must not change ladder, history, gold or XP");
            Check(Failure(() => p.CancelUnstarted(seen)) is InvalidOperationException, "a cleared match cannot be cancelled again");
        }
        using (var p = new LocalProfile(path))
        {
            int history = p.History.Count;
            p.RecoverInterrupted();
            Check(p.History.Count == history && p.Pending == null, "restart must not abandon a cleared start");
            var next = p.BeginLaunch("mage", "priest", 3, record => { });
            Check(p.Pending?.Id == next.Id, "the next start must not be blocked");
            Check(Failure(() => p.CancelUnstarted("other-id")) is InvalidOperationException && p.Pending?.Id == next.Id,
                "only the matching Pending may be cancelled");
            p.Settle(next.Id, "Lost", 1, true);
            p.AcknowledgeRewardPresentation(next.Id);
        }
        // A cancel that cannot commit keeps the Pending (startup recovery still settles it) and reports both errors.
        using (var p = new LocalProfile(path))
        {
            var failure = Failure(() => p.BeginLaunch("mage", "priest", 4, record => {
                p.CommitFailure = point => { if (point == "write") throw new IOException("injected"); };
                throw new InvalidOperationException("stage failed");
            }));
            p.CommitFailure = null;
            Check(failure is InvalidOperationException && failure.Message.Contains("stage failed")
                && failure.Message.Contains("could not be cleared") && failure.InnerException?.Message == "stage failed"
                && p.Pending != null && Read(path).Pending?.Id == p.Pending.Id, "failed cancel must keep Pending and report both errors");
            p.CancelUnstarted(p.Pending.Id);
            Check(p.Pending == null && Read(path).Pending == null, "retry clears the unstarted Pending");
        }
        // Stage timeout / failed engine start: the staged launch succeeded but the engine game never started, then the app
        // quits. The bridge cancels first; if that cannot happen, recovery cancels the unstarted Pending, never Abandoned.
        string stalled = Path.Combine(root, "stage-timeout.xml");
        using (var p = new LocalProfile(stalled))
        {
            var ladder = p.Ladder;
            int history = p.History.Count, stars = p.Stars, gold = p.Gold;
            var xp = p.ClassXp.ToDictionary(row => row.Key, row => row.Value);
            var staged = p.BeginLaunch("mage", "priest", 5, record => { });
            Check(p.Pending?.Unstarted == true && Read(stalled).Pending?.Unstarted == true, "launch Pending must be durably unstarted");
            p.CancelUnstarted(staged.Id);   // the bridge's stage-timeout cancel
            Check(p.Pending == null && Read(stalled).Pending == null, "stage timeout must leave no Pending");
            Check(NoPenalty(p, history, stars, gold, ladder, xp), "stage timeout must not change ladder, history, gold or XP");
        }
        using (var p = new LocalProfile(stalled))
        {
            var ladder = p.Ladder;
            int history = p.History.Count, stars = p.Stars, gold = p.Gold;
            var xp = p.ClassXp.ToDictionary(row => row.Key, row => row.Value);
            var crashed = p.BeginLaunch("mage", "priest", 6, record => { });
            Check(Read(stalled).Pending?.Id == crashed.Id, "crash fixture lost its Pending");
        }
        using (var p = new LocalProfile(stalled))   // reboot after a crash or a cancel that could not commit
        {
            var ladder = p.Ladder;
            int history = p.History.Count, stars = p.Stars, gold = p.Gold;
            var xp = p.ClassXp.ToDictionary(row => row.Key, row => row.Value);
            Check(p.Pending != null && p.Pending.Unstarted, "unstarted Pending must survive a reload");
            p.RecoverInterrupted();
            Check(p.Pending == null && Read(stalled).Pending == null && p.History.All(match => match.Result != "Abandoned"),
                "recovery must cancel, not abandon, an unstarted Pending");
            Check(NoPenalty(p, history, stars, gold, ladder, xp), "recovering an unstarted Pending must not change ladder, history, gold or XP");
        }
        // A started match: refuses cancel, still settles Abandoned on recovery, and old saves (no Unstarted element) read as started.
        using (var p = new LocalProfile(stalled))
        {
            var started = p.BeginLaunch("mage", "priest", 7, record => { });
            p.MarkStarted(started.Id);
            Check(!p.Pending.Unstarted && !File.ReadAllText(stalled).Contains("<Unstarted>"), "MarkStarted must clear the durable marker");
            Check(Failure(() => p.CancelUnstarted(started.Id)) is InvalidOperationException && p.Pending?.Id == started.Id,
                "a started Pending must refuse CancelUnstarted");
            int history = p.History.Count;
            p.RecoverInterrupted();
            Check(p.Pending == null && p.History.Count == history + 1 && p.History.Last().Id == started.Id
                && p.History.Last().Result == "Abandoned", "a started match must still recover as Abandoned");
        }
        using (var p = new LocalProfile(stalled))
        {
            var plain = p.Begin("mage", "priest", 8);   // direct Begin (old saves, tests): always started
            Check(!plain.Unstarted && !File.ReadAllText(stalled).Contains("<Unstarted>"), "Begin must not write an Unstarted marker");
            Check(Failure(() => p.CancelUnstarted(plain.Id)) is InvalidOperationException && p.Pending?.Id == plain.Id,
                "a Pending not committed by BeginLaunch must refuse CancelUnstarted");
            p.RecoverInterrupted();
            Check(p.History.Last().Id == plain.Id && p.History.Last().Result == "Abandoned", "an old-style Pending must recover as Abandoned");
        }
        Console.WriteLine("PASS start trap: launch failure after Begin clears Pending with no ladder change");
    }
}
