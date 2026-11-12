using System.Reflection;
using System.Xml.Serialization;
using SabberStoneCore.Model;

if (args.Length == 2 && args[0] == "--deck-premium-owner-save")
{
    DeckPremiumChecks.OwnerSave(args[1]);
    return;
}

if (args.SequenceEqual(new[] { "--deck-premiums-only" }))
{
    var deckPremiumRoot = Path.Combine(Path.GetTempPath(), "hs-deck-premiums-" + Guid.NewGuid());
    Directory.CreateDirectory(deckPremiumRoot);
    try { DeckPremiumChecks.Run(deckPremiumRoot); } finally { Directory.Delete(deckPremiumRoot, true); }
    return;
}

if (args.SequenceEqual(new[] { "--standard-batches-only" }))
{
    var batchRoot = Path.Combine(Path.GetTempPath(), "hs-standard-batches-" + Guid.NewGuid());
    Directory.CreateDirectory(batchRoot);
    try { StandardBatchChecks.Run(batchRoot); }
    finally { Directory.Delete(batchRoot, true); }
    return;
}

if (args.SequenceEqual(new[] { "--premium-tracks-only" }))
{
    var trackRoot = Path.Combine(Path.GetTempPath(), "hs-premium-tracks-" + Guid.NewGuid());
    Directory.CreateDirectory(trackRoot);
    try { PremiumTrackChecks.Run(trackRoot); }
    finally { Directory.Delete(trackRoot, true); }
    return;
}

if (args.SequenceEqual(new[] { "--grind-tiers-only" }))
{
    string grindRoot = Path.Combine(Path.GetTempPath(), "hs-grind-tiers-" + Guid.NewGuid());
    Directory.CreateDirectory(grindRoot);
    try { GrindTierChecks.Run(grindRoot); }
    finally { Directory.Delete(grindRoot, true); }
    return;
}

if (args.Length == 0)
{
    var premiumRoot = Path.Combine(Path.GetTempPath(), "hs-deck-premiums-" + Guid.NewGuid());
    Directory.CreateDirectory(premiumRoot);
    try { DeckPremiumChecks.Run(premiumRoot); } finally { Directory.Delete(premiumRoot, true); }
    CraftingChecks.Run();
    TurnTimerOptionChecks.Run();
    FpsCounterOptionChecks.Run();
    DeckInferenceOptionChecks.Run();
    MasteryChecks.Run();
    CurrentV14ProgressionChecks.Run();
    // The gate runs only this no-argument path; keep the save-catalog/cheat checks in it.
    var gateCatalogRoot = Path.Combine(Path.GetTempPath(), "hs-profile-catalog-" + Guid.NewGuid());
    Directory.CreateDirectory(gateCatalogRoot);
    try { RewardV5Checks.Run(gateCatalogRoot); MatchTimingChecks.Run(gateCatalogRoot); GrindTierChecks.Run(gateCatalogRoot); ProfileCatalogChecks.Run(gateCatalogRoot); StartupSaveChecks.Run(gateCatalogRoot); SaveHoldChecks.Run(gateCatalogRoot); HiddenLoanerChecks.Run(gateCatalogRoot); TestLoanerGateChecks.Run(gateCatalogRoot); MassPackChecks.Run(gateCatalogRoot); PremiumPackChecks.Run(gateCatalogRoot); DeckRecordChecks.Run(gateCatalogRoot); }
    finally { Directory.Delete(gateCatalogRoot, true); }
    return;
}

static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
static void Reject(Action action, string message)
{
    try { action(); } catch (Exception error) when (error is InvalidOperationException || error is ArgumentException
        || error is IOException || error is UnauthorizedAccessException || error is TargetInvocationException) { return; }
    throw new Exception("Expected rejection: " + message);
}
static LocalProfileData Read(string path)
{
    using var stream = File.OpenRead(path);
    return (LocalProfileData)new XmlSerializer(typeof(LocalProfileData)).Deserialize(stream);
}
static void Write(string path, LocalProfileData data)
{
    using var stream = File.Create(path);
    new XmlSerializer(typeof(LocalProfileData)).Serialize(stream, data);
}
static LocalMatchRecord Historical(string deck, string result, bool conceded = false)
{
    var facts = new LocalRewardFacts();
    var reward = LocalEconomy.Reward(result, 12, conceded, facts);
    return new LocalMatchRecord { Id = Guid.NewGuid().ToString(), Deck = deck, Opponent = "mage", Seed = 1,
        StartedUtc = "2026-10-18T00:00:00Z", Result = result, Conceded = conceded, Turns = 12,
        Gold = reward.Gold, RewardVersion = 2, RewardFacts = facts };
}
// Live-test helper (docs/agent-workflows/live-test-hooks.md): writes a fresh current-version save (welcome gift included)
// with the given grind tier, for lanes 1-3, which have no save picker and seed missing saves from an old fixture.
if ((args.Length == 2 || args.Length == 3) && args[0] == "--new-profile")
{
    if (File.Exists(args[1])) throw new InvalidOperationException("Refusing to overwrite " + args[1]);
    var options = new ProfileOptions { GrindTier = args.Length == 3 ? args[2] : GrindTiers.Default.Id };
    using (var profile = new LocalProfile(args[1], options)) profile.PersistNewProfile(options);
    using (var profile = new LocalProfile(args[1], options)) Console.WriteLine("NEW_PROFILE " + args[1] + " grindTier=" + profile.GrindTier.Id);
    return;
}
// Explicit disposable-copy proof; never point this at the owner's original save.
if (args.Length == 2 && args[0] == "--economy-save-copy-proof")
{
    EconomySaveChecks.Run(args[1]);
    return;
}
if (args.Length == 3 && args[0] == "--hero-draft-roundtrip")
{
    HeroDraftChecks.Run(args[2], args[1] == "create");
    return;
}
if (args.Length == 1 && args[0] == "--profile-catalog")
{
    var catalogRoot = Path.Combine(Path.GetTempPath(), "hs-profile-catalog-" + Guid.NewGuid());
    Directory.CreateDirectory(catalogRoot);
    try { ProfileCatalogChecks.Run(catalogRoot); StartupSaveChecks.Run(catalogRoot); SaveHoldChecks.Run(catalogRoot); }
    finally { Directory.Delete(catalogRoot, true); }
    return;
}
// The legacy body: the checks the no-argument run above does not cover (full Premium fixtures, Reward V3, Death Knight,
// the migration/XP fixtures, hero drafts, test loaners and NPC records). tests/run.py runs it as the `progression-extended` suite.
// Checks the no-argument run already covers (profile catalog, startup saves, backups, review regressions, seen flags,
// deck records, the V14 contract) are not repeated here, so every check runs in exactly one CI suite.
if (args.SequenceEqual(new[] { "--extended" }))
{
    var root = Path.Combine(Path.GetTempPath(), "hs-core-" + Guid.NewGuid());
    Directory.CreateDirectory(root);
    try
    {
        using (var v11 = new LocalProfile(Path.Combine(root, "v11-new.xml"))) v11.CreateDraft("V11", "HERO_08");
        Check(Read(Path.Combine(root, "v11-new.xml")).Version == 15, "new profiles use the current save version");
        PremiumChecks.Run(root);
        RewardV3Checks.Run();
        DeathKnightChecks.Run(root);
        // V7 XP survives the faster curve; reconciliation is additive and atomic.
        string curvePath = Path.Combine(root, "curve-v7.xml");
        using (var profile = new LocalProfile(curvePath)) profile.CreateDraft("Curve V7", "HERO_08");
        var curveSave = Read(curvePath);
        curveSave.CoreScheduleVersion = 7;
        curveSave.ClassXp.Single(entry => entry.Hero == "HERO_08").Xp = 1000;
        curveSave.CoreGrants.Add(new LocalCardCount { Id = "CORE_UNKNOWN_EARNED", Count = 2 });
        Write(curvePath, curveSave);
        byte[] curveBytes = File.ReadAllBytes(curvePath);
        Directory.CreateDirectory(curvePath + ".tmp");
        Reject(() => { using var profile = new LocalProfile(curvePath); }, "curve reconciliation failure");
        Check(File.ReadAllBytes(curvePath).SequenceEqual(curveBytes), "failed curve reconciliation changed save");
        Directory.Delete(curvePath + ".tmp");
        using (var profile = new LocalProfile(curvePath))
            Check(profile.ClassXp["HERO_08"] == 1000 && profile.ClassLevel("HERO_08") == 10, "curve reload preserves XP");
        var curveAfter = Read(curvePath);
        Check(curveAfter.Version == 15 && curveAfter.CoreScheduleVersion == LocalCoreProgression.ScheduleVersion
            && curveSave.CoreGrants.All(old => curveAfter.CoreGrants.Any(card => card.Id == old.Id && card.Count >= old.Count))
            && LocalCoreProgression.Owned(curveAfter.ClassXp.ToDictionary(entry => entry.Hero, entry => entry.Xp), false)
                .All(pair => curveAfter.CoreGrants.Any(card => card.Id == pair.Key && card.Count == pair.Value))
            && curveAfter.CoreGrants.Count > curveSave.CoreGrants.Count, "curve reconciliation only adds eligible grants");
        byte[] curveCommitted = File.ReadAllBytes(curvePath);
        using (var profile = new LocalProfile(curvePath)) { }
        Check(File.ReadAllBytes(curvePath).SequenceEqual(curveCommitted), "curve stable restart");

        HeroDraftChecks.Run(Path.Combine(root, "hero-drafts.xml"), true);
        // (V1/V5/V6 migration, backup and candidate fixtures removed: saves older than V13 are no longer supported.)
        // Class XP follows the save's pace tier (new saves default to 2x), so every XP figure below is scaled the same way.
        int winXp = GrindTiers.Default.ClassXp(LocalCoreProgression.WinXp);
        int lossXp = GrindTiers.Default.ClassXp(LocalCoreProgression.LossXp), tieXp = GrindTiers.Default.ClassXp(LocalCoreProgression.TieXp);
        string path;
        path = Path.Combine(root, "new-class.xml");
        using (var profile = new LocalProfile(path))
        {
            Check(profile.ClassXp.Count == 11 && profile.ClassXp.Values.All(xp => xp == 0), "fresh V8 XP");
            profile.Begin("mage", "priest", 1);
        }
        var newClass = Read(path); newClass.Pending.DeckHero = "HERO_10"; Write(path, newClass);
        using (var profile = new LocalProfile(path))
        {
            var result = profile.Settle(profile.Pending.Id, "Won", 12, false);
            Check(profile.ClassXp["HERO_10"] == winXp && profile.ClassXp.Where(x => x.Key != "HERO_10").All(x => x.Value == 0), "new class XP settlement");
            Check(profile.ProgressionSummary(result).Contains("Demon Hunter XP +" + winXp), "new class XP text");
        }
        path = Path.Combine(root, "settlement.xml");
        string settledId;
        var rewardFacts = new LocalRewardFacts { DistinctCardsPlayed = 6, EnemyMinionsDefeated = 5, MinimumHealth = 5 };
        using (var profile = new LocalProfile(path))
        {
            var pending = profile.Begin("mage", "priest", 1);
            settledId = pending.Id;
            byte[] pendingBytes = File.ReadAllBytes(path);
            Directory.CreateDirectory(path + ".tmp");
            Reject(() => profile.Settle(pending.Id, "Won", 12, false, rewardFacts), "atomic XP settlement failure");
            Check(profile.ClassXp["HERO_08"] == 0 && profile.Gold == 0 && profile.UnopenedPacks == 0 && profile.Pending.Id == pending.Id
                && File.ReadAllBytes(path).SequenceEqual(pendingBytes), "failed settlement advanced XP");
            Directory.Delete(path + ".tmp");
            var result = profile.Settle(pending.Id, "Won", 12, false, rewardFacts);
            // Current rewards (V6) pay gold and dust only: the old bonus facts and great-win pack no longer exist.
            Check(profile.ClassXp["HERO_08"] == winXp && profile.ClassLevel("HERO_08") == LocalCoreProgression.Level(winXp)
                && profile.Gold == result.Gold && profile.DustBalance == result.DustReward && result.BonusGold == 0 && !result.PackReward
                && result.Gold > 0 && result.DustReward > 0 && profile.UnopenedPacks == 0, "XP/gold/dust must settle together");
            Check(LocalRewardPresentation.Summary(result, profile).Body.Contains("Mage XP +" + winXp)
                && LocalRewardPresentation.Summary(result, profile).Body.Contains("Level up: 1 to " + LocalCoreProgression.Level(winXp)), "native summary omits XP/level");
            profile.Settle(pending.Id, "Won", 12, false, rewardFacts);
            Check(profile.ClassXp["HERO_08"] == winXp, "settlement retry doubled XP");
        }
        using (var profile = new LocalProfile(path))
        {
            Check(profile.ClassXp["HERO_08"] == winXp && profile.PendingRewardPresentation.Id == settledId, "restart lost settlement");
            profile.Settle(settledId, "Won", 12, false, rewardFacts);
            Directory.CreateDirectory(path + ".tmp");
            Reject(() => profile.AcknowledgeRewardPresentation(settledId), "ack failure");
            Check(profile.ClassXp["HERO_08"] == winXp && profile.PendingRewardPresentation != null, "ack failure changed XP/receipt");
            Directory.Delete(path + ".tmp");
            profile.AcknowledgeRewardPresentation(settledId);
            Check(!profile.AcknowledgeRewardPresentation(settledId) && profile.ClassXp["HERO_08"] == winXp, "ack replay changed XP");
            foreach (var sample in new[] { ("Lost", false, winXp + lossXp), ("Lost", true, winXp + lossXp), ("Tied", false, winXp + lossXp + tieXp) })
            {
                var pending = profile.Begin("mage-frost", "priest", 1);
                profile.Settle(pending.Id, sample.Item1, 12, sample.Item2);
                profile.AcknowledgeRewardPresentation(pending.Id);
                Check(profile.ClassXp["HERO_08"] == sample.Item3, "loss/concede/tie XP");
            }
            var draft = profile.CreateDraft("Personal priest", "HERO_09");
            profile.SaveDraft(draft.Id, draft.Name, draft.Hero, LocalDecks.Get("priest").CardIds, 0);
            var custom = profile.Begin("custom:" + draft.Id, "mage", 1);
            profile.SaveDraft(draft.Id, "Now warrior", "HERO_01", Array.Empty<string>(), 0);
            profile.Settle(custom.Id, "Won", 12, false);
            profile.AcknowledgeRewardPresentation(custom.Id);
            Check(profile.ClassXp["HERO_09"] == winXp && profile.ClassXp["HERO_01"] == 0, "match must capture hero before draft edit");
            profile.Begin("mage", "priest", 1);
            profile.RecoverInterrupted();
            Check(profile.ClassXp["HERO_08"] == winXp + lossXp + tieXp, "interrupted match awarded XP");
            var xpCopy = (Dictionary<string, int>)profile.ClassXp;
            xpCopy["HERO_08"] = 99999;
            Check(profile.ClassXp["HERO_08"] == winXp + lossXp + tieXp, "XP leaked mutable state");
        }
        Check(LocalCoreProgression.LevelThresholds.Length == 60, "60 class levels");
        for (int i = 1; i < LocalCoreProgression.LevelThresholds.Length; i++)
        {
            int threshold = LocalCoreProgression.LevelThresholds[i];
            Check(LocalCoreProgression.Level(threshold - 1) == i && LocalCoreProgression.Level(threshold) == i + 1, "XP boundary curve");
        }
        Check(LocalCoreProgression.Level(0) == 1 && LocalCoreProgression.Level(1000) == 10
            && LocalCoreProgression.Level(25190) == 60 && LocalCoreProgression.Level(int.MaxValue) == 60, "XP cap");
        var zero = LocalCoreProgression.Heroes.ToDictionary(hero => hero, hero => 0);
        Check(LocalCoreProgression.Heroes.Select(LocalCoreProgression.ClassName).SequenceEqual(new[] {
            "Mage", "Paladin", "Priest", "Warrior", "Druid", "Hunter", "Rogue", "Shaman", "Warlock", "Demon Hunter", "Death Knight"
        }), "class XP display names");
        var full = LocalCoreProgression.Heroes.ToDictionary(hero => hero, hero => 1000);
        Check(LocalCoreProgression.Owned(full, false).Count == 289, "all eleven classes at level 10 must unlock all 289 cards");
        Check(LocalCoreProgression.Owned(zero, false).Count < 289, "locked Core leaked at level 1");
        Check(LocalCardCatalog.PackPool(1).Count == 39 && LocalCardCatalog.PackPool(2).Count == 169
            && LocalCardCatalog.PackPool(2).All(card => !LocalCoreProgression.ById.ContainsKey(card.Id)), "Core contaminated packs");
        var almost = new Dictionary<string, int>(full);
        foreach (string hero in LocalCoreProgression.Heroes.Skip(4)) almost[hero] = 0;
        almost["HERO_01"] = 810;
        string finalNeutral = LocalCoreProgression.Unlocks.Last(card => card.Hero == "").Id;
        Check(!LocalCoreProgression.Owned(almost, false).ContainsKey(finalNeutral)
            && LocalCoreProgression.Owned(full, false).ContainsKey(finalNeutral), "neutral unlock must require combined level 40");
        for (int level = 1; level <= 10; level++)
        {
            var progress = new Dictionary<string, int>(zero);
            foreach (string hero in LocalCoreProgression.Heroes.Take(4)) progress[hero] = LocalCoreProgression.LevelThresholds[level - 1];
            var expected = LocalCoreProgression.Unlocks.Where(card => card.Hero == "" && card.Threshold <= 4 * level).Select(card => card.Id);
            Check(LocalCoreProgression.Owned(progress, false).Keys.Where(id => LocalCoreProgression.ById[id].Hero == "").SequenceEqual(expected), "old neutral combined value changed");
            progress["HERO_10"] = 40;
            expected = LocalCoreProgression.Unlocks.Where(card => card.Hero == "" && card.Threshold <= 4 * level + 1).Select(card => card.Id);
            Check(LocalCoreProgression.Owned(progress, false).Keys.Where(id => LocalCoreProgression.ById[id].Hero == "").SequenceEqual(expected), "new class must advance neutral");
        }
        var available = LocalCoreProgression.AvailableCards;
        bool source = Cards.All.Any(card => card.Id == "CORE_CS2_024");
        Check(available.Count == (source ? 289 : 0), "engine-aware catalog must omit unknown Core cards");
        Check(LocalEconomy.Catalog.Count == 252 + available.Count, "catalog extension");
        Check(available.All(card => NativeUIProjection.ForcesCorePlayability(true, card.Id))
            && !available.Any(card => NativeUIProjection.ForcesCorePlayability(false, card.Id))
            && !NativeUIProjection.ForcesCorePlayability(true, "CS2_024")
            && !NativeUIProjection.ForcesCorePlayability(true, null)
            && LocalCoreProgression.Unlocks.Where(card => available.All(x => x.Id != card.Id))
                .All(card => !NativeUIProjection.ForcesCorePlayability(true, card.Id)), "Core playability bypass must cover exactly supported Core unlocks");
        path = Path.Combine(root, "ownership.xml");
        using (var profile = new LocalProfile(path))
        {
            var draft = profile.CreateDraft("Mage Core", "HERO_08");
            if (source)
            {
                var locked = LocalCoreProgression.Unlocks.First(card => card.Hero == "HERO_08" && card.Threshold > 1);
                profile.SaveDraft(draft.Id, draft.Name, draft.Hero, new[] { locked.Id }, 0);
                Check(!profile.IsDeckComplete(draft.Id), "locked Core deck remains incomplete");
                Check(profile.CustomDecks.Any(d => d.Id == draft.Id && d.Cards.Contains(locked.Id)), "locked Core slots remain visible");
                Check(1000 % winXp == 0, "level 10 is a whole number of wins");
                for (int i = 0; i < 1000 / winXp; i++)
                {
                    var pending = profile.Begin("mage", "priest", i);
                    var result = profile.Settle(pending.Id, "Won", 1, false);
                    if (i == 0) {
                        Check(result.ClassXpBefore == 0 && result.ClassXpAfter == winXp && result.UnlockedCardIds.Count > 0,
                            "settlement captures XP and unlocked card ids");
                        var receiptSnapshot = Read(path).Matches.Last();
                        Check(receiptSnapshot.UnlockedCardIds.SequenceEqual(result.UnlockedCardIds) && receiptSnapshot.ProgressionRuleVersion == LocalCoreProgression.ScheduleVersion,
                            "progression receipt persisted with settlement");
                    }
                    if (i == 0) Check(LocalRewardPresentation.Summary(result, profile).Body.Contains("Core unlocked:"), "summary lacks unlock names");
                    profile.AcknowledgeRewardPresentation(pending.Id);
                }
                Check(profile.ClassLevel("HERO_08") == 10, "the wins worth 1000 XP must reach level 10");
                Check(LocalCoreProgression.Unlocks.Where(card => card.Hero == "HERO_08")
                    .All(card => profile.Collection[card.Id] == card.Copies), "level 10 class ownership");
                profile.SaveDraft(draft.Id, draft.Name, draft.Hero, new[] { locked.Id, locked.Id }, 0);
                string legendary = LocalCoreProgression.Unlocks.First(card => card.Hero == "HERO_08" && card.Copies == 1).Id;
                Reject(() => profile.SaveDraft(draft.Id, draft.Name, draft.Hero, new[] { legendary, legendary }, 0), "legendary copy cap");
                string other = LocalCoreProgression.Unlocks.First(card => card.Hero == "HERO_04").Id;
                Reject(() => profile.SaveDraft(draft.Id, draft.Name, draft.Hero, new[] { other }, 0), "off-class Core");
                var deck = LocalDecks.Get("mage").CardIds
                    .Where(id => LocalCardCatalog.CopyGroup(id) != locked.CopyGroup).Take(28).ToList();
                deck.AddRange(new[] { locked.Id, locked.Id });
                profile.SaveDeck("Complete Core", "HERO_08", deck);
                NativeUIProjection.ValidateCustomSelection(draft.Id, "HERO_08", deck.ToArray(), "HERO_08", deck.ToArray(),
                    profile.Collection.ToDictionary(pair => pair.Key, pair => pair.Value));
                Check(Read(path).Collection.All(card => !LocalCoreProgression.ById.ContainsKey(card.Id)), "derived copies persisted");
            }
            else Reject(() => LocalDecks.Draft("Unavailable", "HERO_08", new[] { "CORE_CS2_024" }), "vendor Core draft");
        }
        using (var profile = new LocalProfile(path))
            Check(!source || profile.ClassXp["HERO_08"] == 1000 && profile.CustomDecks.Single().Cards.Count == 30,
                "restart lost derived deck ownership");
        var native = new Dictionary<string, int> { { "CORE_CS2_024", 1 } };
        var derived = new Dictionary<string, int> { { "CORE_CS2_024", 2 } };
        var grants = NativeUIProjection.CoreGrantCounts(derived, id => native.TryGetValue(id, out int count) ? count : 0);
        Check(grants["CORE_CS2_024"] == 1, "partial native refresh must add only missing copies");
        native["CORE_CS2_024"] += grants["CORE_CS2_024"];
        Check(NativeUIProjection.CoreGrantCounts(derived, id => native[id]).Count == 0, "native refresh retry duplicates Core");
        Reject(() => NativeUIProjection.CoreGrantCounts(derived, id => 3), "native Core overgrant");
        var saved = Read(path);
        // A profile produced by the source engine must remain readable on the vendor engine.
        saved.ClassXp.ForEach(entry => entry.Xp = 2700);
        saved.CustomDecks[0].Cards = new() { "CORE_CS2_024" };
        Write(path, saved);
        using (var profile = new LocalProfile(path))
            Check(profile.CustomDecks.Count == (source ? 1 : 0), "unavailable saved Core drafts must be hidden, preserved");
        Check(Read(path).CustomDecks[0].Cards.Single() == "CORE_CS2_024", "downgrade destroyed draft");
        var invalidXp = Read(path);
        invalidXp.ClassXp[0].Xp = -1; Write(path, invalidXp);
        Reject(() => { using var profile = new LocalProfile(path); }, "negative XP");
        invalidXp.ClassXp[0].Xp = 0; invalidXp.ClassXp[1].Hero = invalidXp.ClassXp[0].Hero; Write(path, invalidXp);
        Reject(() => { using var profile = new LocalProfile(path); }, "duplicate XP class");
        Write(path, saved);
        var malformed = Read(path);
        malformed.ClassXp.RemoveAt(0); Write(path, malformed);
        Reject(() => { using var profile = new LocalProfile(path); }, "missing XP class");
        Write(path, saved); malformed = Read(path);
        malformed.ClassXp[0].Hero = "HERO_99"; Write(path, malformed);
        Reject(() => { using var profile = new LocalProfile(path); }, "unknown XP class");
        Write(path, saved);
        var badReceipt = Read(path);
        badReceipt.LastPack = new LocalPackResult { PoolVersion = 2,
            Cards = Enumerable.Range(0, 5).Select(_ => new LocalPackCard { Id = "CORE_CS2_024" }).ToList() };
        Write(path, badReceipt);
        Reject(() => { using var profile = new LocalProfile(path); }, "Core in pack receipt");
        saved.Collection.Add(new LocalCardCount { Id = "CORE_CS2_024", Count = 2 }); Write(path, saved);
        Reject(() => { using var profile = new LocalProfile(path); }, "stored Core ownership");
        Console.WriteLine("PASS Core progression: " + (source ? "source 289 cards" : "vendor graceful degradation")
            + "; exact backup/failure/restart, eleven-class XP, neutral compatibility, ownership/decks, reward summary");
        TestLoanerChecks.Run(root);
        NpcRecordChecks.Run(root);
    }
    finally { Directory.Delete(root, true); }
}
else throw new ArgumentException("unknown arguments: " + string.Join(" ", args));
