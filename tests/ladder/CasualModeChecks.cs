using System.Xml.Serialization;

// #37 Standard Casual: a Casual match pays Ranked's rewards and class XP but never moves the ladder (stars, hidden rating,
// placement, Legend, milestones, season counts) or win portraits; only the hidden Casual rating moves. Mode survives reload,
// interleaves with ranked receipts, and an old ranked-only save stays byte-identical.
internal static class CasualModeChecks
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
    static LocalMatchRecord Play(LocalProfile p, string result, int seed, bool casual, double? npcRating = 1450, int turns = 10, bool conceded = false)
    {
        var match = p.Begin("mage", "priest", seed, npcRating == null ? null : "npc.g.0001", npcRating == null ? null : "priest",
            npcRating, casual ? CasualRules.Mode : null);
        var settled = p.Settle(match.Id, result, turns, conceded);
        p.AcknowledgeRewardPresentation(match.Id);
        return settled;
    }
    static bool SameLadder(LadderPosition a, LadderPosition b) => a.StarIndex == b.StarIndex && a.BestStarIndex == b.BestStarIndex
        && a.Multiplier == b.Multiplier && a.StreakWins == b.StreakWins && a.PlacementGames == b.PlacementGames && a.Rating == b.Rating
        && a.LegendReached == b.LegendReached;

    internal static void Run(string root)
    {
        // Rules: Elo on the Casual rating only; ties change nothing; the seed is the ranked hidden rating.
        Check(CasualRules.Apply(1400, "Won", 1400) == 1416 && CasualRules.Apply(1400, "Lost", 1400) == 1384
            && CasualRules.Apply(1400, "Tied", 1400) == 1400 && CasualRules.Apply(1400, "Abandoned", 1400) == 1384,
            "Casual Elo: K=32, ties unchanged, abandon is a loss");
        Check(CasualRules.Apply(CasualRules.MaxRating, "Won", 600) == CasualRules.MaxRating, "Casual rating is clamped");
        Reject(() => CasualRules.Apply(double.NaN, "Won", 1400), "NaN Casual rating");

        string path = Path.Combine(root, "casual-ledger.xml");
        using (var p = new LocalProfile(path))
        {
            Play(p, "Won", 1, casual: false);
            var ladder = p.Ladder;
            int gold = p.Gold, stars = p.Stars;
            long dust = p.DustBalance;
            int xp = p.ClassXp["HERO_08"];
            Check(p.CasualRating == CasualRules.Seed(ladder.Rating), "unplayed Casual rating is the ranked seed");

            var win = Play(p, "Won", 2, casual: true);
            Check(win.Mode == CasualRules.Mode && win.Ladder == null && win.TierRewards == null && win.Casual != null,
                "Casual win carries a Casual receipt and no ladder or tier receipt");
            Check(SameLadder(p.Ladder, ladder) && p.Stars == stars, "Casual win moves no stars, hidden rating or placement");
            var roll = LocalEconomy.RewardV6("Won", 10, false, LocalEconomy.RewardRollSeed(win.Id, win.Seed));
            Check(win.Gold == p.GrindTier.Gold(roll.Gold) && win.DustReward == p.GrindTier.Dust(roll.Dust) && p.Gold == gold + win.Gold && p.DustBalance == dust + win.DustReward,
                "Casual win pays the Ranked reward roll");
            Check(win.ClassXpAfter - win.ClassXpBefore == p.GrindTier.ClassXp(LocalCoreProgression.Xp("Won", false)) && p.ClassXp["HERO_08"] == xp + p.GrindTier.ClassXp(LocalCoreProgression.Xp("Won", false)),
                "Casual win pays class XP");
            Check(win.Casual.RatingBefore == CasualRules.Seed(ladder.Rating) && win.Casual.OpponentRating == 1450
                && win.Casual.RatingAfter == CasualRules.Apply(win.Casual.RatingBefore, "Won", 1450) && p.CasualRating == win.Casual.RatingAfter,
                "Casual rating seeded from the ranked rating and moved by Elo against the NPC");

            // Idempotent replay returns the saved Casual receipt; a different opponent rating is a conflict.
            var replay = p.Settle(win.Id, "Won", 10, false);
            Check(replay.Casual.RatingAfter == win.Casual.RatingAfter && p.History.Count == 2 && p.CasualRating == win.Casual.RatingAfter,
                "re-Settle of a Casual match is idempotent");
            Reject(() => p.Settle(win.Id, "Won", 10, false, null, 1700), "conflicting Casual opponent rating");

            foreach (var (result, conceded) in new[] { ("Lost", false), ("Lost", true), ("Tied", false) })
            {
                double before = p.CasualRating;
                var row = Play(p, result, 3, casual: true, conceded: conceded);
                var reward = LocalEconomy.RewardV6(result, 10, conceded, LocalEconomy.RewardRollSeed(row.Id, row.Seed));
                Check(row.Ladder == null && SameLadder(p.Ladder, ladder) && row.Gold == p.GrindTier.Gold(reward.Gold) && row.DustReward == p.GrindTier.Dust(reward.Dust)
                    && p.CasualRating == CasualRules.Apply(before, result, 1450), "Casual " + result + (conceded ? " (concede)" : "") + " moves no rank");
            }

            // Abandon (crash mid-match): a Casual loss, never a ladder loss.
            double beforeAbandon = p.CasualRating;
            var started = p.Begin("mage", "priest", 4, "npc.g.0001", "priest", 1450, CasualRules.Mode);
            Check(p.Pending.Mode == CasualRules.Mode && p.Pending.Casual == null, "pending Casual match records its mode only");
        }
        using (var p = new LocalProfile(path))
        {
            var ladderBefore = p.Ladder;
            p.RecoverInterrupted();
            var abandoned = p.History.Last();
            Check(abandoned.Result == "Abandoned" && abandoned.Mode == CasualRules.Mode && abandoned.Ladder == null
                && SameLadder(p.Ladder, ladderBefore) && abandoned.Casual.RatingAfter < abandoned.Casual.RatingBefore,
                "abandoned Casual match is a Casual loss, not a ladder loss");

            // Ranked after Casual: the ladder replays from its own last receipt, untouched by the Casual rows between.
            var ranked = Play(p, "Won", 5, casual: false);
            Check(ranked.Ladder != null && ranked.Ladder.StarsBefore == ladderBefore.StarIndex && ranked.Ladder.RatingBefore == ladderBefore.Rating
                && ranked.Ladder.PlacementBefore == ladderBefore.PlacementGames, "ranked match continues the ranked ledger");
            Check(p.RankedHistory.Count == 2 && p.CasualHistory.Count == 5 && p.History.Count == 7, "history split by mode");

            // Results: no rank scoop, a Casual line, season counts over ranked games only.
            var history = p.History;
            var casualReceipt = LocalResultsReceipt.FromHistory(history, history[2]);
            Check(casualReceipt.Casual && !casualReceipt.HasRank && casualReceipt.RankText == "Casual · no rank change"
                && casualReceipt.TierRewards.Count == 0 && casualReceipt.SeasonGamesBefore == 1 && casualReceipt.SeasonGamesAfter == 1
                && casualReceipt.SeasonWinsBefore == 1 && casualReceipt.SeasonWinsAfter == 1, "Casual results receipt");
            var rankedReceipt = LocalResultsReceipt.FromHistory(history, history.Last());
            Check(!rankedReceipt.Casual && rankedReceipt.HasRank && rankedReceipt.SeasonGamesBefore == 1 && rankedReceipt.SeasonGamesAfter == 2,
                "ranked receipt after Casual games counts ranked games only");

            // Mode preference per save.
            Check(!p.PrefersCasual, "Standard is the default play mode");
            p.SetPreferredPlayMode(true);
        }
        using (var p = new LocalProfile(path))
        {
            Check(p.PrefersCasual && p.History.Count(m => m.Mode == CasualRules.Mode) == 5, "mode and preference survive reload");
            p.SetPreferredPlayMode(false);
            Check(!p.PrefersCasual, "preference switches back");
        }

        // Tamper rejection: a ladder receipt on a Casual row, a broken Casual chain, an unknown mode.
        var saved = Read(path);
        string control = Path.Combine(root, "casual-control.xml");
        Write(control, LocalProfile.Clone(saved));
        using (var p = new LocalProfile(control)) Check(p.CasualHistory.Count == 5, "untampered rewrite loads (tamper control)");
        foreach (var (name, tamper) in new (string, Action<LocalProfileData>)[] {
            ("ladder receipt on Casual", d => d.Matches.First(m => m.Mode != null).Ladder = d.Matches.First(m => m.Mode == null).Ladder),
            ("broken Casual chain", d => d.Matches.Last(m => m.Mode != null).Casual.RatingBefore += 5),
            ("Casual rating mismatch", d => d.CasualRating += 5),
            ("unknown mode", d => d.Matches.First(m => m.Mode != null).Mode = "Wild"),
            ("Casual receipt on ranked", d => d.Matches.First(m => m.Mode == null).Casual = new LocalCasualReceipt { RatingBefore = 1400, RatingAfter = 1400, OpponentRating = 1400 }),
            ("Casual mode dropped", d => d.Matches.First(m => m.Mode != null).Mode = null),
        })
        {
            string copy = Path.Combine(root, "casual-tamper.xml");
            var data = LocalProfile.Clone(saved);
            tamper(data);
            Write(copy, data);
            Reject(() => new LocalProfile(copy).Dispose(), name);
        }

        // Ranked-only saves never write the new elements (old saves stay byte-identical).
        string rankedPath = Path.Combine(root, "casual-ranked-only.xml");
        using (var p = new LocalProfile(rankedPath)) Play(p, "Won", 6, casual: false);
        string xml = File.ReadAllText(rankedPath);
        Check(!xml.Contains("<Mode") && !xml.Contains("<Casual") && !xml.Contains("<CasualRating") && !xml.Contains("<PlayMode"),
            "ranked-only save writes no Casual elements");

        // Casual first, then ranked: a fresh ladder is untouched by the earlier Casual game.
        string firstPath = Path.Combine(root, "casual-first.xml");
        using (var p = new LocalProfile(firstPath))
        {
            Play(p, "Won", 7, casual: true);
            Check(SameLadder(p.Ladder, LadderRules.Fresh()), "Casual-first profile keeps the fresh ladder");
            var ranked = Play(p, "Won", 8, casual: false);
            Check(ranked.Ladder.StarsBefore == 0 && ranked.Ladder.RatingBefore == LadderRules.StartRating && ranked.Ladder.PlacementBefore == 0,
                "first ranked match after Casual starts placement");
        }
        using (var p = new LocalProfile(firstPath)) Check(p.History.Count == 2, "Casual-first profile reloads");

        // Win portraits count Ranked wins only.
        string portraitPath = Path.Combine(root, "casual-portraits.xml");
        using (var p = new LocalProfile(portraitPath))
        {
            for (int i = 0; i < LocalClassRewards.GoldenPortraitWins; i++) Play(p, "Won", 100 + i, casual: true, npcRating: null);
            Check(p.PortraitGrants.Count == 0, "Casual wins never reach a win portrait");
        }
    }
}
