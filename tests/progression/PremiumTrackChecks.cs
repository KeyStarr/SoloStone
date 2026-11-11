using System.Reflection;
using System.Xml.Serialization;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;

internal static class PremiumTrackChecks
{
    private static readonly XmlSerializer Serializer = new(typeof(LocalProfileData));
    private static void Check(bool ok, string message) { if (!ok) throw new Exception("Premium tracks: " + message); }
    private static LocalProfileData Read(string path) { using var file = File.OpenRead(path); return (LocalProfileData)Serializer.Deserialize(file); }
    private static void Write(string path, LocalProfileData data) { using var file = File.Create(path); Serializer.Serialize(file, data); }
    private static string Xml(LocalProfileData data) { using var writer = new StringWriter(); Serializer.Serialize(writer, data); return writer.ToString(); }
    private static void Own(LocalProfileData data, ClassReward row, int premium = 0, bool progression = true)
    {
        premium = premium == 0 ? row.Premium : premium;
        int copies = LocalCrafting.Playset(row.CardId);
        LocalProfile.AddPremium(data, row.CardId, premium, copies);
        if (progression) data.MasteryGrants.Add(new LocalMasteryGrant { CardId = row.CardId, Premium = premium, Copies = copies,
            Wins = 38, Points = 152, ThresholdVersion = LocalCardMastery.ThresholdVersion });
    }
    private static bool Grant(LocalProfileData data) => (bool)typeof(LocalProfile).GetMethod("GrantClassRewards", BindingFlags.NonPublic | BindingFlags.Static)
        .Invoke(null, new object[] { data, null });
    private static LocalAchievementFeats Feats(LocalProfileData data) => LocalAchievementFeats.Create(data.Achievements, data);
    private static LocalFeat Level(LocalProfileData data, int level) => Feats(data).Find("level:0:" + level);
    internal static void Run(string root)
    {
        using var cosmetics = new CosmeticsFixture();
        string oldMeta = LocalClassRewards.ActiveMetaOverride;
        try {
            LocalClassRewards.ActiveMetaOverride = "violet-hold";
            string fresh = Path.Combine(root, "tracks-fresh.xml");
            using (var p = new LocalProfile(fresh)) p.CreateDraft("Track fixture", "HERO_08");
            Resolver(Read(fresh)); Projection(Read(fresh)); AllCardIds(Read(fresh)); Persistence(root, Read(fresh));
#if ENGINE_SOURCE
            Milestones(root);
#endif
        } finally { LocalClassRewards.ActiveMetaOverride = oldMeta; }
        Console.WriteLine("PASS premium tracks: progression-only substitutions/chains/dust, read-only Feats, stable ids/stats/order, atomic reload/settlement, legacy bytes, deferral, milestones persistence");
    }

    private static void Resolver(LocalProfileData fresh)
    {
        var schedule = LocalClassRewards.ActiveRewards.Where(row => row.Hero == "HERO_08").OrderBy(row => row.Level).ToArray();
        var partial = new LocalProfileData();
        partial.ClassRewardGrants.Add(new LocalClassRewardGrant { Hero = "HERO_08", Level = 60, CardId = schedule[0].CardId,
            Premium = schedule[0].Premium, Copies = 1, ScheduleVersion = 1 });
        LocalProfile.AddPremium(partial, schedule[0].CardId, schedule[0].Premium, 1);
        var fill = LocalClassRewards.Resolve(partial, "HERO_08", 11);
        Check(fill.CardId == schedule[0].CardId && fill.Copies == 1 && fill.ScheduledCardId == null, "partial scheduled playset was substituted/overpaid");
        var data = fresh;
        Own(data, schedule[0], progression: false);
        Check(LocalClassRewards.Resolve(data, "HERO_08", 11).CardId == schedule[0].CardId, "pack-only copies substituted");
        Own(data, schedule[0], premium: 2);
        var next = LocalClassRewards.Resolve(data, "HERO_08", 11);
        Check(next.CardId == schedule[1].CardId && next.ScheduledCardId == schedule[0].CardId && next.Dust == 0, "better progression must substitute");
        Own(data, schedule[1]); Own(data, schedule[2]);
        next = LocalClassRewards.Resolve(data, "HERO_08", 11);
        Check(next.CardId == schedule[3].CardId, "chain did not skip several owned rows");
        data.ClassXp.Single(row => row.Hero == "HERO_08").Xp = LocalCoreProgression.LevelThresholds[11];
        Check(Grant(data), "no class grant");
        var paid = data.ClassRewardGrants.Single(row => row.Level == 11);
        Check(paid.CardId == schedule[3].CardId && data.ClassRewardGrants.Single(row => row.Level == 12).CardId == schedule[4].CardId,
            "two levels reused substitute");
        Check(LocalClassRewards.Resolve(data, "HERO_08", 11).CardId == paid.CardId, "claimed reward changed");
        Check(Level(data, 11).Cards.Single().CardId == paid.CardId && Level(data, 11).Earned, "claimed display disagrees");
        foreach (var row in schedule.Where(row => LocalClassRewards.ProgressionAtLeast(data, row.CardId, row.Premium) < LocalCrafting.Playset(row.CardId))) Own(data, row);
        next = LocalClassRewards.Resolve(data, "HERO_08", 13);
        Check(next.Dust == 400 && next.CardId == null && next.Copies == 0 && next.ScheduledCardId == schedule[2].CardId, "exhausted schedule did not pay dust");
        long dust = data.DustBalance;
        data.ClassXp.Single(row => row.Hero == "HERO_08").Xp = LocalCoreProgression.LevelThresholds[12];
        data.PackRevealPending = true;
        Check(!Grant(data) && data.DustBalance == dust && data.ClassRewardGrants.Count == 2, "pending reveal paid class reward");
        data.PackRevealPending = false;
        Check(Level(data, 13).Reward.Dust == 400 && !Level(data, 13).Earned, "dust preview missing");
        Check(Grant(data) && data.DustBalance == dust + 400 && Level(data, 13).Reward.Dust == 400 && Level(data, 13).Earned, "dust not atomic with ledger");
        Check(!Grant(data) && data.DustBalance == dust + 400, "dust paid twice");
        var receipt = new LocalResultsReceipt { ClassRewards = new[] { paid, next } };
        Check(receipt.ProgressionText.Contains("400 dust") && receipt.ProgressionText.Contains(schedule[3].Name)
            && !receipt.ProgressionText.Contains(schedule[0].Name), "results show scheduled card instead of actual reward");
    }

    private static void Projection(LocalProfileData data)
    {
        var pool = LocalCardCatalog.Supported.Concat(LocalShop.StandardCollectionCards)
            .Where(card => card.Collectible && LocalCardCatalog.InActiveMeta(card) && LocalCardMastery.Tiers(card.Id, 0).Any())
            .GroupBy(card => card.Id, StringComparer.Ordinal).Select(group => group.First()).ToArray();
        var fresh = Feats(data);
        Check(LocalCardMastery.WinCounts(data.Matches).Count == 0, "fresh projection already has wins");
        foreach (var sub in fresh.Categories.Single(row => row.Id == "collection").Subcategories.Where(row => row.SectionName == "Card Mastery")) {
            Check(sub.Feats.Count > 0
                && sub.Feats.All(row => row.Progress == 0 && row.Quota > 0 && !row.Earned), "empty/incomplete fresh mastery " + sub.Name);
        }
        Check(fresh.All.Count(row => row.Id.StartsWith("mastery:", StringComparison.Ordinal)) == pool.Length, "fresh mastery pool count");
        var context = new LocalAchievementPendingContext { PersonalDeck = true, EligibleWorldMatch = true,
            MainDeck = new() { new LocalAchievementDeckCard { BaseCardId = "CORE_CS2_029" }, new LocalAchievementDeckCard { BaseCardId = "CS2_182" } } };
        data.Matches.Add(new LocalMatchRecord { Id = Guid.NewGuid().ToString(), Result = "Won", Turns = 4, DeckHero = "HERO_08", AchievementContext = context });
        string before = Xml(data);
        var view = Feats(data); var catalog = LocalAchievementFeats.Create(data.Achievements);
        Check(before == Xml(data), "snapshot mutated profile");
        var classes = view.Categories.Single(row => row.Id == "classes");
        Check(classes.Subcategories.Count == 13 && classes.Subcategories.First().Name == "Class Challenges", "class tabs missing/renamed ids");
        for (int i = 0; i < 11; i++) {
            var hero = LocalCoreProgression.Heroes[i];
            var sub = classes.Subcategories.Single(row => row.Id == "levels:" + i);
            int cores = LocalCoreProgression.Unlocks.Where(row => row.Hero == hero).Select(row => row.Threshold).Distinct().Count();
            Check(sub.Feats.Count == cores + 52 && sub.SectionName == "Level rewards", "missing level/portrait rows " + hero);
            var first = sub.Feats.Single(row => row.Id == "level:" + i + ":1");
            Check(first.Progress == 0 && first.Quota == 0 && first.Earned, "level one cumulative XP threshold");
            var level = sub.Feats.Single(row => row.Id == "level:" + i + ":40");
            Check(level.Progress == 0 && level.Quota == LocalCoreProgression.LevelThresholds[39], "cumulative XP quota");
        }
        var neutral = classes.Subcategories.Single(row => row.Id == "levels:11");
        Check(neutral.Feats.Count == LocalCoreProgression.Unlocks.Where(row => row.Hero == "").Select(row => Math.Max(4, row.Threshold)).Distinct().Count()
            && neutral.Feats.All(row => row.Progress == 4), "All classes combined level");
        var collection = view.Categories.Single(row => row.Id == "collection");
        Check(collection.Subcategories.Count == 13 && collection.Subcategories.Count(row => row.SectionName == "Card Mastery") == 12,
            "class/Neutral mastery tabs beside Library/milestones");
        Check(view.All.Count(row => row.Id.StartsWith("mastery:")) == pool.Length, "mastery omits active cards without wins");
        Check(view.Find("mastery:CORE_CS2_029").Progress == 1 && view.Find("mastery:CS2_182").Progress == 1, "mastery win counts");
        Check(view.Stats.Total == catalog.Stats.Total && view.Stats.AvailablePoints == catalog.Stats.AvailablePoints
            && view.Stats.Percent == catalog.Stats.Percent && view.RecentlyCompleted.Count == catalog.RecentlyCompleted.Count,
            "generated rows changed stats/recent list");
        Check(view.All.Select(row => row.NativeId).Distinct().Count() == view.All.Count
            && view.All.All(row => LocalAchievementFeats.IsNativeAchievementId(row.NativeId))
            && view.All.Where(row => row.Generated).All(row => row.Points == 0 && row.NativeId >= LocalAchievementFeats.LevelIdBase), "generated ids collide with catalog");
        Check(!LocalAchievementFeats.IsNativeAchievementId(LocalAchievementFeats.AchievementIdBase + 100_000)
            && view.Categories.SelectMany(row => row.Subcategories).All(row => LocalAchievementFeats.IsNativeSectionId(row.SectionNativeId)), "native guard ranges");
        Check(view.Find("mastery:CORE_CS2_029").NativeId == LocalAchievementFeats.MasteryIdBase + Cards.FromId("CORE_CS2_029").AssetId * 4 + 1,
            "mastery id does not use DBF and premium");
        var ids = view.All.Select(row => row.NativeId).ToArray();
        Check(ids.SequenceEqual(Feats(data).All.Select(row => row.NativeId)), "ids not stable");
        var reward = LocalClassRewards.ActiveRewards.Single(row => row.Hero == "HERO_08" && row.Level == 11);
        Own(data, reward); var preview = Level(data, 11);
        Check(preview.Cards.Single().CardId == LocalClassRewards.Resolve(data, "HERO_08", 11).CardId, "unclaimed preview disagrees");
        data.ClassXp.Single(row => row.Hero == "HERO_08").Xp = LocalCoreProgression.LevelThresholds[10];
        Grant(data);
        Check(Level(data, 11).Cards.Single().CardId == preview.Cards.Single().CardId && Level(data, 11).Earned, "claim changed displayed substitute");
        foreach (int owned in new[] { 12, 13 }) Own(data, LocalClassRewards.ActiveRewards.Single(row => row.Hero == "HERO_08" && row.Level == owned));
        var chain = Feats(data);
        var shown = Enumerable.Range(12, 9).Select(level => string.Join(",", chain.Find("level:0:" + level).Cards.Select(card => card.CardId + ":" + card.Premium))).ToArray();
        Check(shown.Where(text => text != "").Distinct().Count() == shown.Count(text => text != ""), "two levels preview the same substitute");
        data.ClassXp.Single(row => row.Hero == "HERO_08").Xp = LocalCoreProgression.LevelThresholds[19];
        Grant(data);
        Check(shown.SequenceEqual(Enumerable.Range(12, 9).Select(level => data.ClassRewardGrants.Single(row => row.Hero == "HERO_08" && row.Level == level))
            .Select(row => row.Dust > 0 ? "" : row.CardId + ":" + row.Premium)), "chained preview disagrees with settlement");
        data.MasteryGrants.Add(new LocalMasteryGrant { CardId = "CORE_CS2_029", Premium = 1, Copies = 2, Wins = 3, ThresholdVersion = 1 });
        LocalProfile.AddPremium(data, "CORE_CS2_029", 1, 2);
        data.Matches.Last().MasteryGrants = new() { data.MasteryGrants.Last() };
        data.Matches.Last().StartedUtc = "2026-10-29T00:00:00Z";
        view = Feats(data);
        Check(view.Find("mastery:CORE_CS2_029").Earned && view.Find("mastery:CORE_CS2_029").Progress == 1
            && view.Find("mastery:CORE_CS2_029").UnlockedUnixSeconds > 0
            && view.RecentlyCompleted.Count == 0, "completion/date or quiet row");
        foreach (var sub in view.Categories.SelectMany(row => row.Subcategories).Where(row => row.SectionName != null)) {
            if (sub.SectionName == "Card Mastery") {
                var sorted = sub.Feats.OrderBy(row => row.Earned).ThenByDescending(row => row.Progress > 0)
                    .ThenBy(row => Cards.FromId(row.Cards.Single().CardId).Cost)
                    .ThenBy(row => Cards.FromId(row.Cards.Single().CardId).Name, StringComparer.Ordinal).ThenBy(row => row.NativeId);
                Check(sub.Feats.SequenceEqual(sorted), "mastery not sorted by earned-last, activity, cost and name");
                continue;
            }
            Check(sub.Feats.Select(row => row.Earned ? 1 : 0).SequenceEqual(sub.Feats.Select(row => row.Earned ? 1 : 0).OrderBy(row => row)), "completed rows not last");
            var ratios = sub.Feats.Where(row => !row.Earned).Select(row => (double)row.Progress / Math.Max(1, row.Quota));
            Check(ratios.SequenceEqual(ratios.OrderByDescending(row => row)), "rows not sorted by closeness");
        }
    }

    private static void AllCardIds(LocalProfileData data)
    {
        foreach (string meta in new[] { "violet-hold", "black-empire" }) {
            LocalClassRewards.ActiveMetaOverride = meta;
            var ids = LocalShop.StandardCollectionCards.Select(row => row.Id).Distinct(StringComparer.Ordinal).ToArray();
            data.Matches = new() { new LocalMatchRecord { Id = Guid.NewGuid().ToString(), Result = "Won", Turns = 4,
                AchievementContext = new LocalAchievementPendingContext { PersonalDeck = true, EligibleWorldMatch = true,
                    MainDeck = ids.Select(id => new LocalAchievementDeckCard { BaseCardId = id }).ToList() } } };
            var view = Feats(data);
            var pool = LocalCardCatalog.Supported.Concat(LocalShop.StandardCollectionCards)
                .Where(card => card.Collectible && LocalCardCatalog.InActiveMeta(card) && LocalCardMastery.Tiers(card.Id, 0).Any())
                .Select(card => "mastery:" + card.Id).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal);
            Check(view.All.Where(row => row.Id.StartsWith("mastery:", StringComparison.Ordinal)).Select(row => row.Id)
                .OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(pool), "all-card mastery pool mismatch " + meta);
            Check(view.All.Select(row => row.NativeId).Distinct().Count() == view.All.Count, "all-card ids collide " + meta);
            Check(view.All.All(row => LocalAchievementFeats.IsNativeAchievementId(row.NativeId)), "all-card ids outside ranges " + meta);
            var missing = view.All.Where(row => row.Generated).SelectMany(row => row.Cards).Where(row => row.DbfId <= 0).Select(row => row.CardId).ToArray();
            Check(missing.SequenceEqual(meta == "black-empire" ? new[] { "BE_EVENT_103" } : Array.Empty<string>()),
                "unexpected missing native card preview " + meta + ": " + string.Join(",", missing));
        }
        LocalClassRewards.ActiveMetaOverride = "violet-hold";
    }

    private static void Persistence(string root, LocalProfileData fresh)
    {
        string path = Path.Combine(root, "tracks-substitute.xml");
        var schedule = LocalClassRewards.ActiveRewards.Where(row => row.Hero == "HERO_08").OrderBy(row => row.Level).ToArray();
        Own(fresh, schedule[0]); fresh.ClassXp.Single(row => row.Hero == "HERO_08").Xp = LocalCoreProgression.LevelThresholds[10];
        Write(path, fresh); byte[] before = File.ReadAllBytes(path);
        foreach (string stage in new[] { "write", "flush", "replace" }) {
            bool failed = false;
            try { using var p = new LocalProfile(path, point => { if (point == stage) throw new IOException("injected tracks"); }); }
            catch (IOException) { failed = true; }
            Check(failed && before.SequenceEqual(File.ReadAllBytes(path)), "reload failure changed bytes " + stage);
        }
        using (var p = new LocalProfile(path)) Check(p.ClassRewardGrants.Single().ScheduledCardId == schedule[0].CardId, "substitute not persisted");
        before = File.ReadAllBytes(path);
        using (var p = new LocalProfile(path)) { var view = LocalAchievementFeats.Create(p.Achievements, p.FeatsSnapshot()); }
        Check(before.SequenceEqual(File.ReadAllBytes(path)), "projection/restart rewrote profile");
        var retired = Read(path); string retiredPath = Path.Combine(root, "tracks-legacy-unknown.xml");
        retired.ClassRewardGrants.Add(new LocalClassRewardGrant { Hero = "HERO_08", Level = 12, CardId = "UNKNOWN_OLD", Premium = 3, Copies = 2, ScheduleVersion = 99 });
        retired.PremiumCollection.Add(new LocalCardCount { Id = "UNKNOWN_OLD", Premium = 3, Count = 2, Acquired = 2 });
        Write(retiredPath, retired);
        using (var p = new LocalProfile(retiredPath)) {
            Check(p.ClassRewardGrants.Any(row => row.CardId == "UNKNOWN_OLD" && row.Copies == 2), "historical unknown grant rejected");
            Check(LocalAchievementFeats.Create(p.Achievements, p.FeatsSnapshot()).Find("level:0:12").RewardText.Contains("UNKNOWN_OLD"), "historical unknown grant preview");
        }
        var data = Read(path); var grant = data.ClassRewardGrants.Single();
        Check(grant.CardId == schedule[1].CardId && grant.Dust == 0, "substitute ledger roundtrip");
        foreach (var row in schedule.Where(row => LocalClassRewards.ProgressionAtLeast(data, row.CardId, row.Premium) < LocalCrafting.Playset(row.CardId))) Own(data, row);
        Write(path, data);
        using (var p = new LocalProfile(path)) {
            var pending = p.Begin("mage", "priest", 820);
            p.CommitChange(next => { next.ClassXp.Single(row => row.Hero == "HERO_08").Xp = LocalCoreProgression.LevelThresholds[11] - LocalCoreProgression.Xp("Won", false); return true; });
            before = File.ReadAllBytes(path); long dust = p.DustBalance;
            foreach (string stage in new[] { "write", "flush", "replace" }) {
                p.CommitFailure = point => { if (point == stage) throw new IOException("settlement tracks"); };
                bool failed = false; try { p.Settle(pending.Id, "Won", 4, false); } catch (IOException) { failed = true; }
                Check(failed && before.SequenceEqual(File.ReadAllBytes(path)) && p.DustBalance == dust && p.Pending.Id == pending.Id, "settlement failure leaked dust/claim");
            }
            p.CommitFailure = null;
            var match = p.Settle(pending.Id, "Won", 4, false);
            Check(match.ClassRewardGrants.Single().Dust == 400 && p.DustBalance == dust + 400 + match.DustReward, "dust settlement not atomic");
            Check(LocalResultsReceipt.FromHistory(p.History, match).ProgressionText.Contains("400 dust") && p.ProgressionSummary(match).Contains("400 dust"), "dust missing in results");
            long paid = p.DustBalance; p.Settle(pending.Id, "Won", 4, false); Check(p.DustBalance == paid, "settlement retry paid dust twice");
        }
        var persisted = Read(path).ClassRewardGrants.Single(row => row.Level == 12);
        Check(persisted.Dust == 400 && persisted.ScheduledCardId == schedule[1].CardId && persisted.CardId == null, "dust roundtrip");
        using (var p = new LocalProfile(path)) Check(p.ClassRewardGrants.Count == 2, "dust restart duplicated grant");
        string legacy = Path.Combine(root, "tracks-legacy.xml");
        using (var p = new LocalProfile(legacy)) p.CommitChange(next => { next.ClassXp.Single(row => row.Hero == "HERO_08").Xp = 1300; return true; });
        using (var p = new LocalProfile(legacy)) { }
        var oldRows = Read(legacy);
        foreach (var row in oldRows.ClassRewardGrants) row.GrantedUtc = null;
        Write(legacy, oldRows);
        before = File.ReadAllBytes(legacy);
        Check(!System.Text.Encoding.UTF8.GetString(before).Contains("GrantedUtc")
            && !System.Text.Encoding.UTF8.GetString(before).Contains("ScheduledCardId") && !System.Text.Encoding.UTF8.GetString(before).Contains("<Dust>"), "optional fields changed older save");
        using (var p = new LocalProfile(legacy)) { }
        Check(before.SequenceEqual(File.ReadAllBytes(legacy)), "legacy bytes changed");
        foreach (var corrupt in new Action<LocalProfileData>[] {
            save => save.ClassRewardGrants.Single(row => row.Dust > 0).Dust = 399,
            save => save.ClassRewardGrants.Single(row => row.Dust > 0).Copies = 1,
            save => save.ClassRewardGrants.Single(row => row.Dust > 0).ScheduledCardId = null,
            save => save.Matches.Last().ClassRewardGrants.Single().Dust = 0,
            save => save.ClassRewardGrants.Single(row => row.Dust > 0).GrantedUtc = "bad"
        }) {
            var invalid = Read(path); corrupt(invalid);
            string bad = Path.Combine(root, "tracks-corrupt-" + Guid.NewGuid() + ".xml"); Write(bad, invalid);
            byte[] bytes = File.ReadAllBytes(bad); bool rejected = false;
            try { using var p = new LocalProfile(bad); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected && bytes.SequenceEqual(File.ReadAllBytes(bad)), "invalid substitute/dust ledger accepted");
        }
    }

#if ENGINE_SOURCE
    private static bool IsMilestone(string id) => new[] { "first-signature-mastery", "first-diamond-mastery", "fifty-golden-masteries", "all-premium-deck" }.Contains(id);
    private static void Milestones(string root)
    {
        string path = Path.Combine(root, "tracks-milestones.xml");
        using (var p = new LocalProfile(path)) {
            var ids = LocalShop.StandardCollectionCards.Select(card => card.Id).Take(50).ToArray();
            p.CommitChange(next => {
                foreach (string id in ids) next.MasteryGrants.Add(new LocalMasteryGrant { CardId = id, Premium = 1, Copies = LocalCrafting.Playset(id), Wins = 10, ThresholdVersion = 1 });
                foreach (int premium in new[] { 3, 2 }) next.MasteryGrants.Add(new LocalMasteryGrant { CardId = "CATA_213", Premium = premium, Copies = 1, Wins = 30, ThresholdVersion = 1 });
                return true;
            });
            var cards = LocalShop.StandardCollectionCards.Where(c => c.Rarity == Rarity.COMMON && c.Type != CardType.HERO && !LocalCardCatalog.IsCore(c.Id)
                && (c.Class == CardClass.MAGE || c.Class == CardClass.NEUTRAL) && LocalDecks.IsAllowedInDeck(c, CardClass.MAGE))
                .GroupBy(c => c.Id).Select(group => group.First()).OrderBy(c => c.Id, StringComparer.Ordinal).Take(15).Select(c => c.Id).ToArray();
            p.CommitChange(next => {
                foreach (string id in cards) {
                    var owned = next.Collection.SingleOrDefault(row => row.Id == id);
                    if (owned == null) { owned = new LocalCardCount { Id = id }; next.Collection.Add(owned); }
                    owned.Count = owned.Acquired = 2;
                }
                return true;
            });
            var deck = p.CreateDraft("Premium feat", "HERO_08");
            var deckIds = cards.SelectMany(id => new[] { id, id }).ToArray();
            p.SaveDraft(deck.Id, deck.Name, deck.Hero, deckIds, 0);
            var played = new LocalDeck("Premium feat", "HERO_08", deckIds);
            for (int repeat = 0; repeat < 2; repeat++) {
                var context = p.AchievementContextFor(played, true, "npc-tracks", 2500, "Diamond", deckIds.Select(id => new LocalDeckCard { Id = id, Premium = 3 }));
                var pending = p.BeginLaunch("custom:" + deck.Id, "priest", repeat, "npc-tracks", "priest", 2500, context, _ => { });
                var game = new Game(new GameConfig { RandomSeed = 73, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
                    History = true, Logging = false, FormatType = FormatType.FT_WILD, Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.PRIEST,
                    Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(), Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
                game.StartGame(); game.Process(ConcedeTask.Any(game.Player2));
                var frozen = pending.AchievementContext;
                var evidence = new LocalAchievementMatchEvidence(pending.Id, "Won", frozen.StartingHero, frozen.QueuedNpcId, frozen.QueuedNpcBand,
                    false, true, false, LocalAchievementContextBinding.Create(frozen), new string('A', 64), true,
                    game.Player1.Id, game.Player2.Id, game.Player1.Hero.Id, game.Player2.Hero.Id, game.HistoryFacts.Records);
                var earned = LocalAchievementEvaluator.Evaluate(frozen, evidence, p.Achievements.Career, Array.Empty<string>(),
                    new LocalAchievementLedgerContext(mastery: Read(path).MasteryGrants, turns: 4));
                Check(earned.Earned.Count(row => row.Id == "all-premium-deck") == 1, "premium deck evaluator");
                var missing = p.AchievementContextFor(played, true, "npc-tracks", 2500, "Diamond");
                Check(missing.MainDeck.All(row => row.Premium == 0) && frozen.MainDeck.All(row => row.Premium == 3), "premium not frozen/cloned");
                if (repeat == 0) {
                    var ledger = new LocalAchievementLedgerContext(mastery: Read(path).MasteryGrants, turns: 3);
                    Check(!LocalAchievementEvaluator.Evaluate(frozen, evidence, p.Achievements.Career, Array.Empty<string>(), ledger)
                        .Earned.Any(row => row.Id == "all-premium-deck"), "short game premium feat");
                    Check(!LocalAchievementEvaluator.Evaluate(frozen, evidence.WithConcessions(true, false), p.Achievements.Career,
                        Array.Empty<string>(), new LocalAchievementLedgerContext(mastery: Read(path).MasteryGrants, turns: 4)).Earned.Any(), "concede earned milestones");
                    foreach (bool personal in new[] { true, false }) {
                        var changed = p.AchievementContextFor(played, personal, "npc-tracks", 2500, "Diamond",
                            personal ? null : deckIds.Select(id => new LocalDeckCard { Id = id, Premium = 1 }));
                        changed.MatchId = pending.Id;
                        var changedEvidence = new LocalAchievementMatchEvidence(pending.Id, "Won", changed.StartingHero, changed.QueuedNpcId, changed.QueuedNpcBand,
                            false, true, false, LocalAchievementContextBinding.Create(changed), new string('A', 64), true,
                            game.Player1.Id, game.Player2.Id, game.Player1.Hero.Id, game.Player2.Hero.Id, game.HistoryFacts.Records);
                        Check(!LocalAchievementEvaluator.Evaluate(changed, changedEvidence, p.Achievements.Career, Array.Empty<string>(),
                            new LocalAchievementLedgerContext(turns: 4)).Earned.Any(row => row.Id == "all-premium-deck"), "Normal/loaner premium feat");
                    }
                    var skipped = Read(path).MasteryGrants.Select(row => new LocalMasteryGrant { Premium = row.Premium, Copies = 0 });
                    Check(!LocalAchievementEvaluator.Evaluate(frozen, evidence, p.Achievements.Career, Array.Empty<string>(),
                        new LocalAchievementLedgerContext(mastery: skipped, turns: 4)).Earned.Any(row => IsMilestone(row.Id) && row.Id != "all-premium-deck"),
                        "skipped zero-copy tiers earned milestone");
                    byte[] bytes = File.ReadAllBytes(path);
                    foreach (string stage in new[] { "write", "flush", "replace" }) {
                        p.CommitFailure = point => { if (point == stage) throw new IOException("milestone fault"); };
                        bool failed = false; try { p.Settle(pending.Id, "Won", 4, false, null, null, true, evidence); } catch (IOException) { failed = true; }
                        Check(failed && bytes.SequenceEqual(File.ReadAllBytes(path)) && !p.Achievements.Unlocks.Any(row => IsMilestone(row.Id)), "milestone failure leaked grant");
                    }
                    p.CommitFailure = null;
                }
                var match = p.Settle(pending.Id, "Won", 4, false, null, null, true, evidence);
                Check(match.AchievementGrants.Count(row => IsMilestone(row.AchievementId)) == (repeat == 0 ? 4 : 0), "milestones duplicated/missing");
                Check(match.AchievementGrants.Where(row => IsMilestone(row.AchievementId)).All(row => row.Gold == 0 && row.Dust == 0 && row.StandardPacks == 0), "milestones material reward");
                p.AcknowledgeRewardPresentation(match.Id);
            }
        }
        using (var p = new LocalProfile(path)) {
            Check(p.Achievements.Unlocks.Count(row => IsMilestone(row.Id)) == 4 && p.Achievements.Grants.Count(row => IsMilestone(row.AchievementId)) == 4, "milestones not durable");
            var view = LocalAchievementFeats.Create(p.Achievements, p.FeatsSnapshot());
            Check(view.RecentlyCompleted.Count(row => IsMilestone(row.Id)) == 4 && view.All.Where(row => IsMilestone(row.Id)).Sum(row => row.Points) == 85 && view.Find("all-premium-deck").Earned, "milestones not point/toast eligible");
        }
    }
#endif
}
