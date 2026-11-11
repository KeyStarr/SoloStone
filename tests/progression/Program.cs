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
ActiveMetaChecks.Run();
PlayerDefaultChecks.Run();
ShowcaseChecks.Run();
AchievementCatalogChecks.Run();
AchievementEvaluatorContractChecks.Run();
var root = Path.Combine(Path.GetTempPath(), "hs-core-" + Guid.NewGuid());
Directory.CreateDirectory(root);
try
{
    ProfileCatalogChecks.Run(root);
    StartupSaveChecks.Run(root);
    using (var v11 = new LocalProfile(Path.Combine(root, "v11-new.xml"))) v11.CreateDraft("V11", "HERO_08");
    Check(Read(Path.Combine(root, "v11-new.xml")).Version == 12, "new profiles use V11");
    PremiumChecks.Run(root);
    RewardV3Checks.Run(root);
    BackupChecks.Run(root);
    DeathKnightChecks.Run(root);
    ReviewRegressions.Run(root);
    SeenChecks.Run(root);
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
    Check(curveAfter.Version == 12 && curveAfter.CoreScheduleVersion == 8
        && curveSave.CoreGrants.All(old => curveAfter.CoreGrants.Any(card => card.Id == old.Id && card.Count >= old.Count))
        && LocalCoreProgression.Owned(curveAfter.ClassXp.ToDictionary(entry => entry.Hero, entry => entry.Xp), false)
            .All(pair => curveAfter.CoreGrants.Any(card => card.Id == pair.Key && card.Count == pair.Value))
        && curveAfter.CoreGrants.Count > curveSave.CoreGrants.Count, "curve reconciliation only adds eligible grants");
    byte[] curveCommitted = File.ReadAllBytes(curvePath);
    using (var profile = new LocalProfile(curvePath)) { }
    Check(File.ReadAllBytes(curvePath).SequenceEqual(curveCommitted), "curve stable restart");

    HeroDraftChecks.Run(Path.Combine(root, "hero-drafts.xml"), true);
    var path = Path.Combine(root, "migration.xml");
    var matches = new[] { Historical("mage", "Won"), Historical("warrior-control", "Lost"),
        Historical("priest", "Lost", true), Historical("custom:910000010", "Won"),
        Historical("custom:910000099", "Won"), Historical("mage", "Abandoned") };
    var fixture = new LocalProfileData { Version = 5, PackSeed = 17, GoldBalance = matches.Sum(match => match.Gold),
        Matches = matches.ToList(), PendingRewardPresentationId = matches[4].Id,
        Collection = LocalEconomy.StarterCollection.Select(pair => new LocalCardCount { Id = pair.Key, Count = pair.Value }).ToList(),
        NextCustomDeckId = 910000011,
        CustomDecks = new() { new LocalCustomDeck { Id = 910000010, SortOrder = 1, Name = "Old paladin", Hero = "HERO_04" } } };
    var packCollection = fixture.Collection.ToDictionary(card => card.Id, card => card.Count);
    fixture.LastPack = LocalEconomy.GeneratePack(packCollection, fixture.PackSeed, 0, 1);
    fixture.Collection = packCollection.Select(pair => new LocalCardCount { Id = pair.Key, Count = pair.Value }).ToList();
    fixture.GoldBalance += fixture.LastPack.Cards.Sum(card => card.DuplicateGold);
    fixture.PackSequence = 1;
    fixture.PackRevealPending = true;
    Write(path, fixture);
    byte[] original = File.ReadAllBytes(path);
    // Fail after backup, before replacement. Retry must use identical source bytes.
    Directory.CreateDirectory(path + ".tmp");
    Reject(() => { using var ignored = new LocalProfile(path); }, "migration write failure");
    Check(File.ReadAllBytes(path).SequenceEqual(original), "migration failure changed source");
    Check(File.ReadAllBytes(path + ".v5.bak").SequenceEqual(original), "backup must be byte-exact");
    Directory.Delete(path + ".tmp");
    using (var profile = new LocalProfile(path))
    {
        Check(profile.ClassXp["HERO_08"] == 100 && profile.ClassXp["HERO_01"] == 60
            && profile.ClassXp["HERO_09"] == 0 && profile.ClassXp["HERO_04"] == 100, "retroactive XP by class/result");
        Check(profile.PendingRewardPresentation.Id == matches[4].Id, "migration lost pending reward");
        Check(profile.PackRevealPending && profile.PackSequence == 1 && profile.LastPack.PoolVersion == 1
            && profile.LastPack.Cards.Select(card => card.Id).SequenceEqual(fixture.LastPack.Cards.Select(card => card.Id)),
            "V5 migration changed pending pack receipt");
        Check(profile.Gold == fixture.GoldBalance && profile.History.Count == 6, "migration altered economy/history");
        Check(Read(path).Version == 12 && Read(path).Collection.Count == fixture.Collection.Count, "V9 stores XP, not Core copies");
    }
    byte[] migrated = File.ReadAllBytes(path);
    using (var restarted = new LocalProfile(path))
        Check(restarted.ClassXp["HERO_08"] == 100 && restarted.ClassXp["HERO_04"] == 100, "migration replayed XP");
    Check(File.ReadAllBytes(path).SequenceEqual(migrated), "second V8 load rewrote profile");
    var snapshot = Path.Combine(root, "candidate.xml");
    File.WriteAllBytes(snapshot, original);
    Reject(() => typeof(LocalProfile).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
        new[] { typeof(string), typeof(bool) }, null).Invoke(new object[] { snapshot, true }), "candidate migration");
    Check(File.ReadAllBytes(snapshot).SequenceEqual(original) && !File.Exists(snapshot + ".v5.bak"), "candidate changed bytes");

    path = Path.Combine(root, "v6.xml");
    var v6 = Read(Path.Combine(root, "migration.xml"));
    v6.Version = 6; v6.CoreScheduleVersion = 2;
    // This historical fixture predates calibration; its pending match must keep the old reservation.
    v6.LadderCalibration = null; v6.LadderRuleVersion = 0;
    int v6Fold = v6.Matches.Aggregate(0, (stars, match) => LocalProfile.NextStars(stars, match.Result));
    v6.LadderState = new LocalLadderState { StarIndex = v6Fold, BestStarIndex = v6Fold };
    v6.ClassXp = v6.ClassXp.Take(4).ToList();
    v6.ClassXp[0].Xp = 1750; v6.ClassXp[1].Xp = 450;
    v6.ClassXp[2].Xp = 2700; v6.ClassXp[3].Xp = 12345;
    v6.CoreGrants = LocalCoreProgression.Owned(v6.ClassXp.ToDictionary(x => x.Hero, x => x.Xp), false)
        .Where(pair => LocalCoreProgression.ById[pair.Key].Hero == "" || v6.ClassXp.Any(x => x.Hero == LocalCoreProgression.ById[pair.Key].Hero))
        .Select(pair => new LocalCardCount { Id = pair.Key, Count = pair.Value }).ToList();
    v6.CoreGrants.Add(new LocalCardCount { Id = "CORE_UNKNOWN_EARNED", Count = 2 });
    v6.PendingRewardPresentationId = null;
    v6.Pending = new LocalMatchRecord { Id = Guid.NewGuid().ToString(), Deck = "mage", DeckHero = "HERO_08",
        Opponent = "priest", Seed = 2, StartedUtc = "2026-10-18T00:00:00Z", RewardVersion = 2,
        RewardFacts = new LocalRewardFacts() };
    Write(path, v6);
    byte[] v6Bytes = File.ReadAllBytes(path);
    string v6SnapshotPath = Path.Combine(root, "v6-candidate.xml");
    File.WriteAllBytes(v6SnapshotPath, v6Bytes);
    Reject(() => typeof(LocalProfile).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
        new[] { typeof(string), typeof(bool) }, null).Invoke(new object[] { v6SnapshotPath, true }), "V6 candidate migration");
    Check(File.ReadAllBytes(v6SnapshotPath).SequenceEqual(v6Bytes) && !File.Exists(v6SnapshotPath + ".v6.bak"),
        "V6 candidate changed bytes");
    Directory.CreateDirectory(path + ".tmp");
    Reject(() => { using var ignored = new LocalProfile(path); }, "V6 commit failure");
    Check(File.ReadAllBytes(path).SequenceEqual(v6Bytes) && File.ReadAllBytes(path + ".v6.bak").SequenceEqual(v6Bytes),
        "V6 failed migration changed source/backup");
    Directory.Delete(path + ".tmp");
    using (var profile = new LocalProfile(path))
    {
        Check(profile.ClassXp.Count == 11 && v6.ClassXp.All(x => profile.ClassXp[x.Hero] == x.Xp)
            && LocalCoreProgression.Heroes.Skip(4).All(hero => profile.ClassXp[hero] == 0), "V6 migration reset XP");
        Check(profile.Pending.Id == v6.Pending.Id, "V6 pending lost");
    }
    var v7 = Read(path);
    Check(v7.Version == 12 && v7.CoreScheduleVersion == LocalCoreProgression.ScheduleVersion, "V6 version not upgraded");
    Check(v6.CoreGrants.All(old => v7.CoreGrants.Any(grant => grant.Id == old.Id && grant.Count == old.Count)), "V6 earned grants changed");
    var additions = v7.CoreGrants.Where(grant => v6.CoreGrants.All(old => old.Id != grant.Id)).ToArray();
    var expectedAdditions = LocalCoreProgression.Unlocks.Where(card => card.Threshold == 1
        && LocalCoreProgression.Heroes.Skip(4).Contains(card.Hero));
    Check(additions.Select(grant => grant.Id).SequenceEqual(expectedAdditions.Select(card => card.Id))
        && additions.All(grant => grant.Count == LocalCoreProgression.ById[grant.Id].Copies), "V6 migration added wrong grants");
    // Compare every serialized field after accounting for only the three intended changes.
    v6.Version = 12; v6.SelectedHubTheme = "CLASSIC"; v6.CoreScheduleVersion = LocalCoreProgression.ScheduleVersion;
    v6.ClassXp.AddRange(LocalCoreProgression.Heroes.Skip(4).Select(hero => new LocalClassXp { Hero = hero }));
    v6.CoreGrants.AddRange(additions);
    Check(v7.ClassRewardGrants.Count == v6.ClassXp.Sum(row => Math.Max(0, LocalCoreProgression.Level(row.Xp) - 10))
        && v7.PremiumCollection.Count == v7.ClassRewardGrants.Count, "V6 migration adds earned premium rewards");
    v6.ClassRewardGrants = v7.ClassRewardGrants; v6.PremiumCollection = v7.PremiumCollection;
    string expectedPath = Path.Combine(root, "expected-v7.xml"); Write(expectedPath, v6);
    Check(File.ReadAllBytes(expectedPath).SequenceEqual(File.ReadAllBytes(path)), "V6 migration changed other saved state");
    byte[] v7Bytes = File.ReadAllBytes(path);
    using (var profile = new LocalProfile(path)) Check(profile.Pending.Id == v6.Pending.Id, "V8 restart state");
    Check(File.ReadAllBytes(path).SequenceEqual(v7Bytes), "V8 restart changed bytes");
    // Pending match and pending reward presentation cannot coexist; cover the latter separately.
    string v6RewardPath = Path.Combine(root, "v6-reward.xml");
    var v6Reward = Read(Path.Combine(root, "migration.xml"));
    v6Reward.Version = 6; v6Reward.CoreScheduleVersion = 1; v6Reward.LadderCalibration = null;
    v6Reward.ClassXp = v6Reward.ClassXp.Take(4).ToList();
    v6Reward.CoreGrants = v6Reward.CoreGrants.Where(card => !LocalCoreProgression.Heroes.Skip(4)
        .Contains(LocalCoreProgression.ById[card.Id].Hero)).ToList();
    Write(v6RewardPath, v6Reward);
    byte[] v6RewardBytes = File.ReadAllBytes(v6RewardPath);
    using (var profile = new LocalProfile(v6RewardPath))
        Check(profile.PendingRewardPresentation.Id == v6Reward.PendingRewardPresentationId && profile.PackRevealPending,
            "V6 pending presentations lost");
    var v7Reward = Read(v6RewardPath);
    v6Reward.Version = 12; v6Reward.SelectedHubTheme = "CLASSIC"; v6Reward.CoreScheduleVersion = LocalCoreProgression.ScheduleVersion;
    v6Reward.LadderCalibration = new LocalLadderCalibration { Version = 1, MatchCount = v6Reward.Matches.Count };
    v6Reward.ClassXp.AddRange(LocalCoreProgression.Heroes.Skip(4).Select(hero => new LocalClassXp { Hero = hero }));
    v6Reward.CoreGrants.AddRange(v7Reward.CoreGrants.Where(grant => v6Reward.CoreGrants.All(old => old.Id != grant.Id)).ToArray());
    Write(expectedPath, v6Reward);
    Check(File.ReadAllBytes(expectedPath).SequenceEqual(File.ReadAllBytes(v6RewardPath))
        && File.ReadAllBytes(v6RewardPath + ".v6.bak").SequenceEqual(v6RewardBytes), "V6 reward state changed");
    foreach (var corruption in new Action<LocalProfileData>[] {
        profile => profile.ClassXp.Add(new LocalClassXp { Hero = "HERO_06" }),
        profile => profile.Matches[0].DeckHero = "HERO_06",
        profile => profile.Pending.DeckHero = "HERO_06" })
    {
        var badV6 = Read(path); badV6.Version = 6; badV6.ClassXp = badV6.ClassXp.Take(4).ToList();
        corruption(badV6);
        string badPath = Path.Combine(root, "bad-v6-" + Guid.NewGuid() + ".xml"); Write(badPath, badV6);
        Reject(() => { using var profile = new LocalProfile(badPath); }, "frozen V6 validation");
        Check(!File.Exists(badPath + ".v6.bak"), "invalid V6 backed up/migrated");
    }
    // Older migration still preserves original-version backup semantics.
    string v1Path = Path.Combine(root, "v1.xml");
    Write(v1Path, new LocalProfileData { Version = 1 });
    byte[] v1Bytes = File.ReadAllBytes(v1Path);
    using (var profile = new LocalProfile(v1Path)) Check(profile.ClassXp.Count == 11, "V1 XP cardinality");
    Check(Read(v1Path).Version == 12 && File.ReadAllBytes(v1Path + ".v1.bak").SequenceEqual(v1Bytes), "V1 migration/backup");
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
        Check(profile.ClassXp["HERO_10"] == 100 && profile.ClassXp.Where(x => x.Key != "HERO_10").All(x => x.Value == 0), "new class XP settlement");
        Check(profile.ProgressionSummary(result).Contains("Demon Hunter XP +100"), "new class XP text");
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
        Check(profile.ClassXp["HERO_08"] == 100 && profile.ClassLevel("HERO_08") == 3
            && profile.Gold == result.Gold && profile.DustBalance == result.DustReward && result.BonusGold == 20
            && result.Gold + result.DustReward >= 60 && profile.UnopenedPacks == 1, "XP/gold/dust/pack must settle together");
        Check(LocalRewardPresentation.Summary(result, profile).Body.Contains("Mage XP +100")
            && LocalRewardPresentation.Summary(result, profile).Body.Contains("Level up: 1 to 3"), "native summary omits XP/level");
        profile.Settle(pending.Id, "Won", 12, false, rewardFacts);
        Check(profile.ClassXp["HERO_08"] == 100, "settlement retry doubled XP");
    }
    using (var profile = new LocalProfile(path))
    {
        Check(profile.ClassXp["HERO_08"] == 100 && profile.PendingRewardPresentation.Id == settledId, "restart lost settlement");
        profile.Settle(settledId, "Won", 12, false, rewardFacts);
        Directory.CreateDirectory(path + ".tmp");
        Reject(() => profile.AcknowledgeRewardPresentation(settledId), "ack failure");
        Check(profile.ClassXp["HERO_08"] == 100 && profile.PendingRewardPresentation != null, "ack failure changed XP/receipt");
        Directory.Delete(path + ".tmp");
        profile.AcknowledgeRewardPresentation(settledId);
        Check(!profile.AcknowledgeRewardPresentation(settledId) && profile.ClassXp["HERO_08"] == 100, "ack replay changed XP");
        foreach (var sample in new[] { ("Lost", false, 160), ("Lost", true, 160), ("Tied", false, 220) })
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
        Check(profile.ClassXp["HERO_09"] == 100 && profile.ClassXp["HERO_01"] == 0, "match must capture hero before draft edit");
        profile.Begin("mage", "priest", 1);
        profile.RecoverInterrupted();
        Check(profile.ClassXp["HERO_08"] == 220, "interrupted match awarded XP");
        var xpCopy = (Dictionary<string, int>)profile.ClassXp;
        xpCopy["HERO_08"] = 99999;
        Check(profile.ClassXp["HERO_08"] == 220, "XP leaked mutable state");
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
            for (int i = 0; i < 10; i++)
            {
                var pending = profile.Begin("mage", "priest", i);
                var result = profile.Settle(pending.Id, "Won", 1, false);
                if (i == 0) {
                    Check(result.ClassXpBefore == 0 && result.ClassXpAfter == 100 && result.UnlockedCardIds.Count > 0,
                        "settlement captures XP and unlocked card ids");
                    var receiptSnapshot = Read(path).Matches.Last();
                    Check(receiptSnapshot.UnlockedCardIds.SequenceEqual(result.UnlockedCardIds) && receiptSnapshot.ProgressionRuleVersion == LocalCoreProgression.ScheduleVersion,
                        "progression receipt persisted with settlement");
                }
                if (i == 0) Check(LocalRewardPresentation.Summary(result, profile).Body.Contains("Core unlocked:"), "summary lacks unlock names");
                profile.AcknowledgeRewardPresentation(pending.Id);
            }
            Check(profile.ClassLevel("HERO_08") == 10, "10 wins must reach level 10");
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
        + "; V1/V5/V6 migrations, exact backup/failure/restart, eleven-class XP, neutral compatibility, ownership/decks, reward summary");
    TestLoanerChecks.Run(root);
    NpcRecordChecks.Run(root);
    DeckRecordChecks.Run(root);
}
finally { Directory.Delete(root, true); }
