using SabberStoneCore.Model;
using System.Xml.Serialization;

internal static class GrindTierChecks
{
    private static readonly XmlSerializer Xml = new XmlSerializer(typeof(LocalProfileData));
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static void Reject(Action action)
    {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new Exception("Expected invalid grind tier rejection");
    }
    private static LocalProfileData Read(string path) { using var input = File.OpenRead(path); return (LocalProfileData)Xml.Deserialize(input)!; }
    private static void Write(string path, LocalProfileData data) { using var output = File.Create(path); Xml.Serialize(output, data); }
    private static string Progress(LocalProfileData data)
    {
        data.GrindTier = null;
        using var output = new StringWriter(); Xml.Serialize(output, data); return output.ToString();
    }

    private static void FreshShopQuoteBenchmark(string root)
    {
        string source = RepoRoot.Find("tools/grind_pace.py");
        Check(source != null, "find Python pace model in this worktree");
        var start = new System.Diagnostics.ProcessStartInfo("python3") {
            WorkingDirectory = source!, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("import json, sys; from pathlib import Path; sys.path.insert(0, 'tools'); "
            + "import grind_pace; print(json.dumps(grind_pace.load_catalogue(Path('.')).deck_costs))");
        using var python = System.Diagnostics.Process.Start(start)!;
        string output = python.StandardOutput.ReadToEnd(), errors = python.StandardError.ReadToEnd();
        python.WaitForExit();
        Check(python.ExitCode == 0, "Python shop quotes failed: " + errors);
        using var json = System.Text.Json.JsonDocument.Parse(output);
        var expected = json.RootElement.EnumerateArray().ToDictionary(row => row.GetProperty("recipe").GetString()!,
            row => row.GetProperty("shop_gold").GetInt32());
        using var fresh = new LocalProfile(Path.Combine(root, "fresh-shop-quotes.xml"));
        Check(fresh.ClassXp.Values.All(xp => xp == 0) && fresh.FeatsSnapshot().Welcome == null, "quote new player before one-time grants");
        var products = LocalShop.Default.Products.Where(p => p.Kind == "Deck").ToArray();
        Check(expected.Count == products.Length, "Python prices every shipped shop deck");
        foreach (var product in products)
            Check(fresh.PurchasePrice(product.Id) == expected[product.RecipeId], "Python equals production PurchasePrice: " + product.Id);
        var prices = products.Select(p => fresh.PurchasePrice(p.Id)).OrderBy(price => price).ToArray();
        Check(prices.Length == 72 && (prices[35] + prices[36]) / 2d == 4080 && prices[0] == 940 && prices[71] == 9360,
            "fresh-profile shop quote median/min/max");
        Console.WriteLine("PASS Python/production fresh-profile PurchasePrice: all 72 decks, median 4080 gold, range 940-9360");
    }

    internal static void Run(string root)
    {
        Check(GrindTiers.Selectable.Count == 4 && GrindTiers.Selectable.Select(t => t.Id).SequenceEqual(new[] { "relaxed-6x", "standard-2x", "grinder", "retail" }), "four ordered unique tiers");
        Check(GrindTiers.All.Count == 7 && GrindTiers.All.Select(t => t.Id).Distinct().Count() == 7
            && GrindTiers.Find("relaxed") == GrindTiers.Relaxed && GrindTiers.Find("standard") == GrindTiers.Standard,
            "legacy ids remain known, hidden from both pickers");
        Check(GrindTiers.Relaxed.RewardScale == 1.5 && GrindTiers.Relaxed.ClassXpScale == 1.5
            && GrindTiers.Standard.RewardScale == 1 && GrindTiers.Standard.ClassXpScale == 1
            && ReferenceEquals(GrindTiers.Relaxed.Ladder, GrindTiers.Relaxed2x.Ladder)
            && ReferenceEquals(GrindTiers.Standard.Ladder, GrindTiers.Standard2x.Ladder), "legacy scales frozen and successor ladders identical");
        Check(GrindTiers.Standard2x.RewardScale == 2 && GrindTiers.Standard2x.ClassXpScale == 2
            && ReferenceEquals(GrindTiers.Standard2x.Ladder, LadderPace.Standard), "standard doubled income/XP and unchanged ladder");
        Check(GrindTiers.Relaxed2x.RewardScale == 3 && GrindTiers.Relaxed2x.ClassXpScale == 3, "relaxed doubled income/XP");
        Check(GrindTiers.Relaxed6x.RewardScale == 6 && GrindTiers.Relaxed6x.ClassXpScale == 3
            && GrindTiers.Relaxed6x.MasteryPointsPerWin == 6
            && ReferenceEquals(GrindTiers.Relaxed6x.Ladder, GrindTiers.Relaxed.Ladder), "new Dad-gamer reward-only retune");
        Check(GrindTiers.Successor(GrindTiers.RelaxedId) == GrindTiers.Relaxed6x
            && GrindTiers.Successor(GrindTiers.Relaxed2xId) == GrindTiers.Relaxed6x, "both Dad-gamer current tiers upgrade");
        Check(GrindTiers.Retail.RewardScale == 0.4 && GrindTiers.Retail.ClassXpScale == 0.4
            && ReferenceEquals(GrindTiers.Retail.Ladder, LadderPace.Standard), "Retail calibrated income and Standard ladder");
        Check(ReferenceEquals(GrindTiers.Find(null), GrindTiers.Standard) && ReferenceEquals(GrindTiers.Find(""), GrindTiers.Standard), "absent tier defaults");
        Reject(() => GrindTiers.Find("unknown"));
        Check(GrindTiers.Relaxed2x.Gold(45) == 135 && GrindTiers.Grinder.Gold(45) == 34, "half away from zero rounding");
        // Results text shows the stars the match's own tier paid: Grinder has no bonus stars, Relaxed scales them.
        LocalResultsReceipt Rank(GrindTier pace) => new LocalResultsReceipt { Result = "Won", Pace = pace, Ladder = new LocalLadderReceipt {
            StarsBefore = 20, StarsAfter = 21, StarsGained = 1, MultiplierBefore = 6, MultiplierAfter = 6, LadderRuleVersion = 2 } };
        Check(Rank(GrindTiers.Standard2x).RankText.Contains("bonus stars x6") && Rank(GrindTiers.Relaxed2x).RankText.Contains("bonus stars x9")
            && !Rank(GrindTiers.Grinder).RankText.Contains("bonus stars"), "results bonus text uses the stamped pace");
        // Cross-check Python's shipped-deck benchmark against resolved runtime lists and the real crafting boundary.
        var deckCosts = LocalShop.Default.Products.Where(p => p.Kind == "Deck").Select(product => {
            var recipe = LocalShop.Default.Recipe(product.RecipeId);
            return recipe.Cards.Concat(recipe.Sideboards.SelectMany(board => board.Cards))
                .Sum(card => LocalCrafting.TryGetPrice(card.Id, out int cost, out _) ? card.Count * cost : 0);
        }).OrderBy(cost => cost).ToArray();
        Check(deckCosts.Length == 72 && (deckCosts[35] + deckCosts[36]) / 2d == 7490
            && deckCosts[0] == 1160 && deckCosts[71] == 18520, "resolved shop decks match collection model craft-cost benchmark");
        FreshShopQuoteBenchmark(root);
        string dir = Path.Combine(root, "grind-tiers"); Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "profile.xml");
        using (var profile = new LocalProfile(path))
        {
            Check(profile.GrindTier == GrindTiers.Standard2x, "new save defaults to new Normie");
            profile.PersistNewProfile(new ProfileOptions { GrindTier = GrindTiers.Relaxed6xId });
        }
        LocalMatchRecord first;
        using (var profile = new LocalProfile(path))
        {
            Check(profile.GrindTier == GrindTiers.Relaxed6x && Read(path).GrindTier == GrindTiers.Relaxed6xId, "creation choice survives reopen");
            first = Win(profile, GrindTiers.Relaxed6x, 71);
            Check(first.Ladder != null && first.Casual == null, "ranked relaxed receipt");
            var summary = LocalRewardPresentation.Summary(first, profile);
            var achievements = LocalAchievementPresentation.Build(first.AchievementGrants);
            Check(summary.Items.Where(item => item.Kind == LocalRewardKind.Gold).Sum(item => item.Amount) == first.Gold + first.RivalGold + achievements.Gold
                && summary.Items.Where(item => item.Kind == LocalRewardKind.Dust).Sum(item => item.Amount) == first.DustReward + achievements.Dust,
                "native reward summary presents the scaled paid currencies");
            Check(summary.Body.Contains("XP +300"), "class XP summary uses receipt pace");
        }
        using (var profile = new LocalProfile(path))
        {
            var before = Read(path); string progress = Progress(before);
            Check(profile.SetGrindTier(GrindTiers.GrinderId), "change commits");
            Check(Progress(Read(path)) == progress, "changing pace preserves every earned field and receipt");
            byte[] bytes = File.ReadAllBytes(path);
            Check(!profile.SetGrindTier(GrindTiers.GrinderId) && File.ReadAllBytes(path).SequenceEqual(bytes), "same pace is idempotent");
            Reject(() => profile.SetGrindTier("unknown"));
            Check(File.ReadAllBytes(path).SequenceEqual(bytes), "invalid change leaves bytes untouched");
        }
        using (var profile = new LocalProfile(path))
        {
            Check(profile.GrindTier == GrindTiers.Grinder && profile.History.First().GrindTier == GrindTiers.Relaxed6xId, "new pace and old receipts reopen");
            Win(profile, GrindTiers.Grinder, 72);
            profile.SetGrindTier(GrindTiers.Relaxed6xId);
            var casual = Win(profile, GrindTiers.Relaxed6x, 73, true);
            Check(casual.Ladder == null && casual.Casual != null, "casual scales income without ladder");
            var pending = profile.Begin("player-core-mage", "priest", 74);
            profile.SetGrindTier(GrindTiers.GrinderId);
            byte[] beforeFailure = File.ReadAllBytes(path);
            int count = profile.History.Count;
            profile.CommitFailure = phase => { if (phase == "write") throw new IOException("pace settlement fault"); };
            bool failed = false;
            try { profile.Settle(pending.Id, "Won", 8, false); }
            catch (IOException) { failed = true; }
            finally { profile.CommitFailure = null; }
            Check(failed && profile.Pending.GrindTier == GrindTiers.Relaxed6xId && profile.History.Count == count
                && File.ReadAllBytes(path).SequenceEqual(beforeFailure), "failed scaled settlement preserves pending receipt and all saved balances");
            var settled = profile.Settle(pending.Id, "Won", 8, false);
            CheckPaid(settled, GrindTiers.Relaxed6x);
            Check(settled.GrindTier == GrindTiers.Relaxed6xId, "changing during a pending match keeps its stamp");
            profile.AcknowledgeRewardPresentation(settled.Id);
        }
        using (var profile = new LocalProfile(path))
        {
            Check(profile.GrindTier == GrindTiers.Grinder, "mixed ranked/casual history replay passes");
            profile.SetGrindTier(GrindTiers.RetailId);
            var ranked = Win(profile, GrindTiers.Retail, 76);
            Check(ranked.GrindTier == GrindTiers.RetailId && ranked.Ladder != null, "Retail ranked stamp and receipt");
            var casual = Win(profile, GrindTiers.Retail, 77, true);
            Check(casual.GrindTier == GrindTiers.RetailId && casual.Casual != null, "Retail casual stamp and receipt");
        }
        using (var profile = new LocalProfile(path))
            Check(profile.GrindTier == GrindTiers.Retail && profile.History.Count(m => m.GrindTier == GrindTiers.RetailId) == 2,
                "Retail choice and scaled receipts survive replay");

        var valid = Read(path);
        foreach (bool receipt in new[] { false, true })
        {
            var bad = LocalProfile.Clone(valid);
            if (receipt) bad.Matches[0].GrindTier = "future"; else bad.GrindTier = "future";
            string badPath = Path.Combine(dir, "unknown-" + receipt + ".xml"); Write(badPath, bad);
            Reject(() => { using var ignored = new LocalProfile(badPath); });
        }
        LegacyHistory(dir);

        string catalogDir = Path.Combine(dir, "catalog"); Directory.CreateDirectory(catalogDir);
        // Guard canonical paths use /private on macOS.
        if (catalogDir.StartsWith("/var/", StringComparison.Ordinal)) catalogDir = "/private" + catalogDir;
        string catalog = Path.Combine(catalogDir, "profile.xml");
        var slot = ProfileCatalog.Create(catalog, "Relaxed", new ProfileOptions { GrindTier = GrindTiers.Relaxed6xId });
        string selected = ProfileCatalog.ProfilePathForId(catalog, slot.Id);
        Check(ProfileCatalog.OptionsForProfile(catalog, selected).GrindTier == GrindTiers.Relaxed6xId, "catalog creation option round trip");
        using (var profile = new LocalProfile(selected))
        {
            Check(profile.GrindTier == GrindTiers.Relaxed6x, "catalog passes pace to new profile");
            profile.SetGrindTier(GrindTiers.GrinderId);
        }
        using (var profile = new LocalProfile(selected)) Check(profile.GrindTier == GrindTiers.Grinder, "creation options cannot override live pace");
        Reject(() => ProfileCatalog.Create(catalog, "Bad", new ProfileOptions { GrindTier = "unknown" }));
        Reject(() => ProfileCatalog.Create(catalog, "Empty", new ProfileOptions { GrindTier = "" }));
        Console.WriteLine("PASS grind tier persistence, receipts, ranked and casual");
    }

    private static void LegacyHistory(string dir)
    {
        string seed = Path.Combine(dir, "legacy-seed.xml");
        using (var profile = new LocalProfile(seed))
        {
            profile.SetGrindTier(GrindTiers.RelaxedId);
            Win(profile, GrindTiers.Relaxed, 175);
            profile.SetGrindTier(GrindTiers.StandardId);
            Win(profile, GrindTiers.Standard, 176);
            Win(profile, GrindTiers.Standard, 177);
        }
        var legacy = Read(seed);
        legacy.Matches[2].GrindTier = null; // Before pace choices: the same unscaled Standard payout.
        foreach (string id in new[] { GrindTiers.RelaxedId, GrindTiers.Relaxed2xId, GrindTiers.StandardId, null })
        {
            string path = Path.Combine(dir, "legacy-" + (id ?? "absent") + ".xml");
            var old = LocalProfile.Clone(legacy); old.GrindTier = id; Write(path, old);
            string progress = Progress(Read(path)); byte[] original = File.ReadAllBytes(path);
            bool failed = false;
            try { using var ignored = new LocalProfile(path, phase => { if (phase == "write") throw new IOException("pace upgrade fault"); }); }
            catch (IOException) { failed = true; }
            Check(failed && File.ReadAllBytes(path).SequenceEqual(original), "failed pace upgrade preserves original save bytes");
            var successor = id == GrindTiers.RelaxedId || id == GrindTiers.Relaxed2xId ? GrindTiers.Relaxed6x : GrindTiers.Standard2x;
            using (var profile = new LocalProfile(path))
            {
                Check(profile.GrindTier == successor && Read(path).GrindTier == successor.Id, "legacy current pace upgraded durably");
                Check(Progress(Read(path)) == progress, "pace upgrade changes no earned fields, payouts or match stamps");
                CheckPaid(profile.History[0], GrindTiers.Relaxed);
                CheckPaid(profile.History[1], GrindTiers.Standard);
                CheckPaid(profile.History[2], GrindTiers.Standard);
                Check(profile.History[0].GrindTier == GrindTiers.RelaxedId && profile.History[1].GrindTier == GrindTiers.StandardId
                    && profile.History[2].GrindTier == null, "legacy and absent history stamps preserved");
                Win(profile, successor, 178);
            }
            byte[] after = File.ReadAllBytes(path);
            using (var profile = new LocalProfile(path))
                Check(profile.GrindTier == successor && profile.History.Count == 4 && File.ReadAllBytes(path).SequenceEqual(after),
                    "mixed legacy/successor history reopens idempotently");
        }
        // A match already begun under the old pace must still settle under that stamp after the current pace upgrades.
        string pendingPath = Path.Combine(dir, "legacy-pending.xml");
        using (var profile = new LocalProfile(pendingPath))
        {
            profile.SetGrindTier(GrindTiers.RelaxedId);
            profile.Begin("player-core-mage", "priest", 179);
        }
        using (var profile = new LocalProfile(pendingPath))
        {
            Check(profile.GrindTier == GrindTiers.Relaxed6x && profile.Pending.GrindTier == GrindTiers.RelaxedId, "upgrade preserves pending stamp");
            var settled = profile.Settle(profile.Pending.Id, "Won", 8, false);
            CheckPaid(settled, GrindTiers.Relaxed);
            profile.AcknowledgeRewardPresentation(settled.Id);
            Win(profile, GrindTiers.Relaxed6x, 180);
        }
        using (var profile = new LocalProfile(pendingPath)) Check(profile.History.Count == 2, "legacy pending and successor payouts replay");
        string defaultPath = Path.Combine(dir, "default.xml");
        using (var profile = new LocalProfile(defaultPath)) profile.PersistNewProfile(new ProfileOptions());
        using (var profile = new LocalProfile(defaultPath))
            Check(profile.GrindTier == GrindTiers.Standard2x && Read(defaultPath).GrindTier == GrindTiers.Standard2xId, "new saves persist new Normie by default");
        Console.WriteLine("PASS legacy relaxed/standard/absent history, atomic current-pace upgrade, pending stamp, successor payout and restart");
    }

    private static LocalMatchRecord Win(LocalProfile profile, GrindTier tier, int seed, bool casual = false)
    {
        var pending = profile.Begin("player-core-mage", "priest", seed, null, null, null, casual ? CasualRules.Mode : null);
        Check(pending.GrindTier == tier.Id, "Begin stamps current pace");
        var settled = profile.Settle(pending.Id, "Won", 8, false);
        CheckPaid(settled, tier);
        profile.AcknowledgeRewardPresentation(settled.Id);
        return settled;
    }
    private static void CheckPaid(LocalMatchRecord match, GrindTier tier)
    {
        var roll = LocalEconomy.RewardV6("Won", 8, false, LocalEconomy.RewardRollSeed(match.Id, match.Seed));
        Check(match.Gold == tier.Gold(roll.Gold) && match.DustReward == tier.Dust(roll.Dust), "paid amounts exactly scale V6 roll");
        Check(match.ClassXpAfter - match.ClassXpBefore == tier.ClassXp(100), "class XP uses match pace");
    }
}
