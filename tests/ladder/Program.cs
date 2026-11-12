using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using System.Security.Cryptography;
using System.Xml.Serialization;

if (args.Length == 0)
{
    CurrentV14LadderChecks.Run();
    return;
}
// The legacy body below: the pre-V14 fixtures the default run above does not repeat. tests/run.py runs it as the
// `ladder-extended` suite. Anything else is a typo, not "run the default".
if (!args.SequenceEqual(new[] { "--extended" }))
    throw new ArgumentException("unknown arguments: " + string.Join(" ", args) + " (use no argument or --extended)");

static void Check(bool value, string message) { if (!value) throw new Exception(message); }
static void CheckCollection(LocalProfile profile, int classicCount, int classicCopies, string message)
{
    var owned = profile.Collection;
    // No virtual Fabled/sideboard ownership (owner 2026-10-30): the collection is the ledger plus Core grants.
    var classic = owned.Where(entry => !LocalCardCatalog.IsCore(entry.Key)).ToArray();
    var core = LocalCoreProgression.Owned(profile.ClassXp);
    Check(classic.Length == classicCount && classic.Sum(entry => entry.Value) == classicCopies
        && owned.Count == classicCount + core.Count
        && core.All(entry => owned.TryGetValue(entry.Key, out int copies) && copies == entry.Value), message);
}
static void Reject(Action action, string message)
{
    try { action(); }
    catch (InvalidOperationException) { return; }
    catch (ArgumentException) { return; }
    throw new Exception("Expected rejection: " + message);
}
static void ReplaceFileWithDirectory(string path, out byte[] saved)
{
    saved = File.ReadAllBytes(path);
    File.Delete(path);
    Directory.CreateDirectory(path);
}
static void RestoreFile(string path, byte[] saved)
{
    Directory.Delete(path);
    File.WriteAllBytes(path, saved);
}
static LocalProfileData ReadProfile(byte[] bytes)
{
    using var stream = new MemoryStream(bytes);
    return (LocalProfileData)new XmlSerializer(typeof(LocalProfileData)).Deserialize(stream);
}
static void WriteProfile(string path, LocalProfileData profile)
{
    using var stream = File.Create(path);
    new XmlSerializer(typeof(LocalProfileData)).Serialize(stream, profile);
}
static string MatchSignature(LocalMatchRecord match) => string.Join("|", new object[] {
    match.Id, match.Deck, match.Opponent, match.Seed, match.StartedUtc, match.Result, match.Turns,
    match.Conceded, match.Gold, match.RewardVersion, match.BonusGold, match.PackReward,
    match.RewardFacts?.DistinctCardsPlayed, match.RewardFacts?.EnemyMinionsDefeated,
    match.RewardFacts?.MinimumHealth
});
// The current (V6, rolled) reward of a match: facts no longer matter, a loss or draw pays a third, the grind tier scales it.
static LocalReward Rolled(LocalMatchRecord match, string result, int turns = 12)
{
    var reward = LocalEconomy.RewardV6(result, turns, false, LocalEconomy.RewardRollSeed(match.Id, match.Seed));
    var tier = GrindTiers.Find(match.GrindTier);
    return new LocalReward { Gold = tier.Gold(reward.Gold), BonusGold = reward.BonusGold, PackReward = reward.PackReward, Dust = tier.Dust(reward.Dust) };
}
// A fresh current profile at path holding the given gold (saves on disk are plain XML).
static void GoldProfile(string path, int gold)
{
    using (var fresh = new LocalProfile(path)) fresh.SelectCardBack(0); // a no-change write that creates the file
    var data = ReadProfile(File.ReadAllBytes(path)); data.GoldBalance = gold; WriteProfile(path, data);
}
string root = Path.Combine(Path.GetTempPath(), "hs-ladder-tests-" + Guid.NewGuid());
Directory.CreateDirectory(root);
try
{
    Check(LocalEconomy.PackPrice == 100, "pack price");
    Check(LocalDecks.All.Count == 8 && LocalDecks.All.Select(deck => deck.Id).SequenceEqual(new[] {
        "mage", "warrior", "priest", "mage-frost", "warrior-control", "priest-combo", "paladin", "paladin-control"
    }), "deck catalog order changed");
    var oldDecks = new[] {
        ("mage", DeckStyle.Tempo, "NEW1_012,NEW1_012,EX1_608,EX1_608,CS2_024,CS2_024,CS2_029,CS2_029,CS2_033,CS2_033,CS2_023,CS2_023,CS2_022,CS2_022,CS2_032,CS2_032,EX1_015,EX1_015,EX1_556,EX1_556,EX1_019,EX1_019,CS2_182,CS2_182,CS2_179,CS2_179,CS2_200,CS2_200,EX1_284,EX1_284"),
        ("warrior", DeckStyle.Value, "CS2_106,CS2_106,CS2_114,CS2_114,CS2_108,CS2_108,EX1_606,EX1_606,EX1_400,EX1_400,NEW1_011,NEW1_011,CS2_112,CS2_112,EX1_402,EX1_402,EX1_603,EX1_603,EX1_007,EX1_007,EX1_556,EX1_556,CS2_179,CS2_179,CS2_182,CS2_182,CS2_200,CS2_200,CS2_222,CS2_222"),
        ("priest", DeckStyle.Control, "CS2_235,CS2_235,CS2_004,CS2_004,CS1_130,CS1_130,CS2_234,CS2_234,EX1_622,EX1_622,CS1_112,CS1_112,EX1_091,EX1_091,EX1_335,EX1_335,CS2_181,CS2_181,DS1_055,DS1_055,EX1_066,EX1_066,EX1_019,EX1_019,CS2_179,CS2_179,CS2_182,CS2_182,CS2_200,CS2_200"),
        ("mage-frost", DeckStyle.Frost, "CS2_031,CS2_031,CS2_027,CS2_027,CS2_024,CS2_024,CS2_023,CS2_023,CS2_026,CS2_026,CS2_033,CS2_033,CS2_179,CS2_179,CS2_029,CS2_029,CS2_028,CS2_028,NEW1_021,NEW1_021,EX1_096,EX1_096,EX1_007,EX1_007,EX1_284,EX1_284,CS2_032,EX1_279,EX1_012,EX1_561"),
        ("warrior-control", DeckStyle.Control, "CS2_108,CS2_108,EX1_410,EX1_410,EX1_400,EX1_400,EX1_402,EX1_402,EX1_603,EX1_603,CS2_106,CS2_106,EX1_606,EX1_606,NEW1_011,NEW1_011,EX1_407,EX1_407,EX1_007,EX1_007,EX1_391,EX1_411,EX1_414,CS2_203,EX1_005,EX1_110,EX1_016,EX1_249,EX1_298,EX1_561"),
        ("priest-combo", DeckStyle.Combo, "CS2_235,CS2_235,CS2_004,CS2_004,CS1_129,CS1_129,CS2_236,CS2_236,EX1_621,EX1_621,EX1_591,EX1_591,CS2_181,CS2_181,EX1_335,EX1_335,CS2_234,CS2_234,EX1_622,EX1_622,NEW1_020,NEW1_020,CS2_117,CS2_117,EX1_284,EX1_284,CS2_179,CS2_179,EX1_007,EX1_007")
    };
    foreach (var expected in oldDecks)
    {
        var actual = LocalDecks.Get(expected.Item1);
        Check(actual.Style == expected.Item2 && string.Join(",", actual.CardIds) == expected.Item3,
            "legacy deck changed: " + expected.Item1);
    }
    Check(LocalDecks.Get("paladin").Hero == "HERO_04" && LocalDecks.Get("paladin").Style == DeckStyle.Value &&
        LocalDecks.Get("paladin").CardIds.Count == 30, "Paladin value loaner");
    Check(LocalDecks.Get("paladin-control").Hero == "HERO_04" && LocalDecks.Get("paladin-control").Style == DeckStyle.Control &&
        LocalDecks.Get("paladin-control").CardIds.Count == 30 && LocalDecks.Get("paladin-control").CardIds.Count(id => id == "EX1_366") == 2 &&
        !LocalDecks.Get("paladin-control").CardIds.Contains("CS2_091"), "Paladin control loaner weapons");
    Check(LocalCardCatalog.Current.Count == 252
        && LocalEconomy.Catalog.Count == 252 + LocalCoreProgression.AvailableCards.Count
        && new HashSet<string>(LocalEconomy.Catalog.Select(c => c.Id), StringComparer.Ordinal).SetEquals(
            LocalCardCatalog.Current.Concat(LocalCoreProgression.AvailableCards).Select(c => c.Id)),
        "current catalog is exact Classic catalog plus engine-supported Core unlocks");
    Check(LocalCardCatalog.PackPool(1).Count == 39 && LocalCardCatalog.PackPool(2).Count == 169, "versioned pack pool sizes");
    Check(LocalCardCatalog.Current.Count(c => c.Rarity == Rarity.COMMON) == 64, "common Classic catalog size");
    Check(LocalCardCatalog.Current.Count(c => c.Rarity == Rarity.RARE) == 57, "rare Classic catalog size");
    Check(LocalCardCatalog.Current.Count(c => c.Rarity == Rarity.EPIC) == 21, "epic Classic catalog size");
    Check(LocalCardCatalog.Current.Count(c => c.Rarity == Rarity.LEGENDARY) == 27, "legendary Classic catalog size");
    Check(LocalEconomy.LegacyV2Catalog.Count == 63 && LocalEconomy.LegacyV2Catalog.All(old =>
        LocalEconomy.Catalog.Any(current => current.Id == old.Id)), "legacy V2 catalog is not an exact retained subset");
    Check(LocalEconomy.StarterCollection.Count == 93 && LocalEconomy.StarterCollection.Values.Sum() == 186,
        "starter collection must contain 93 ids and 186 copies");
    string[] paladinFree = { "CS2_087", "CS2_088", "CS2_089", "CS2_091", "CS2_092", "CS2_093", "CS2_094", "CS2_097", "EX1_360", "EX1_371" };
    string[] paladinPack = { "EX1_354", "EX1_355", "EX1_362", "EX1_366", "EX1_382", "EX1_619" };
    Check(paladinFree.All(id => LocalEconomy.StarterCollection.TryGetValue(id, out int count) && count == 2),
        "Paladin basic grants missing");
    Check(paladinPack.All(id => !LocalEconomy.StarterCollection.ContainsKey(id)), "Paladin pack cards were gifted");
    var paladinValue = LocalDecks.Get("paladin");
    Check(paladinValue.CardIds.GroupBy(id => id).All(group =>
        LocalEconomy.StarterCollection.TryGetValue(group.Key, out int owned) && owned >= group.Count()),
        "fresh starter does not own Paladin value loaner");
    Check(LocalDecks.Custom("Silver Line", "HERO_04", paladinValue.CardIds).Build().Count == 30,
        "Paladin custom deck rejected");
    var reachedPaladinPackCards = new HashSet<string>();
    for (int seed = 0; seed < 1000 && reachedPaladinPackCards.Count < paladinPack.Length; seed++)
    {
        var collection = LocalEconomy.StarterCollection.ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var card in LocalEconomy.GeneratePack(collection, seed, 0).Cards)
            if (paladinPack.Contains(card.Id)) reachedPaladinPackCards.Add(card.Id);
    }
    Check(reachedPaladinPackCards.SetEquals(paladinPack), "not all Paladin pack cards are reachable");
    var iceLance = LocalEconomy.Catalog.Single(card => card.Id == "CS2_031");
    Check(iceLance.Text.Contains("@spelldmg"), "Ice Lance fixture no longer exercises engine macro cleanup");
    Check(LocalCollectionUI.RulesText(iceLance) ==
        "Freeze a character. If it was already Frozen, deal 4 damage instead.",
        "card rules leaked engine formatting macros");
    var blessingOfMight = LocalEconomy.Catalog.Single(card => card.Id == "CS2_087");
    Check(blessingOfMight.Text.Contains("_"), "Blessing of Might fixture no longer exercises engine spacing markers");
    Check(LocalCollectionUI.RulesText(blessingOfMight) == "Give a minion +3 Attack.",
        "card rules leaked engine underscore spacing markers");

    string freshPath = Path.Combine(root, "fresh.xml");
    int freshGold;
    using (var store = new LocalProfile(freshPath))
    {
        Check(store.Gold == 0 && store.UnopenedPacks == 0 && store.Stars == 0 && store.Rank == "Bronze 10"
            && store.LastPack == null && !store.PackRevealPending, "fresh zero-pack profile has no receipt to resume");
        CheckCollection(store, 93, 186, "fresh seeded collection plus exact Core grants");
        var exposed = (Dictionary<string, int>)store.Collection;
        exposed.Clear();
        CheckCollection(store, 93, 186, "collection escaped by reference");
        var pending = store.Begin("mage", "priest", 123);
        Check(pending.RewardVersion == LocalEconomy.RollRewardVersion && File.ReadAllText(freshPath).Contains("<Version>15</Version>"), "new profile is not the current version with rolled rewards");
        Reject(() => store.Begin("mage", "warrior", 124), "second pending match");
        var result = store.Settle(pending.Id, "Won", 12, false);
        var rolled = Rolled(pending, "Won"); freshGold = rolled.Gold;
        Check(result.Gold == rolled.Gold && result.DustReward == rolled.Dust && result.BonusGold == 0 && !result.PackReward
            && store.Gold == rolled.Gold && store.DustBalance == rolled.Dust && store.Stars == LadderRules.Fresh().Multiplier,
            "legacy four-argument settlement changed");
        Check(store.Settle(pending.Id, "Won", 12, false).Gold == rolled.Gold && store.Gold == rolled.Gold && store.DustBalance == rolled.Dust, "duplicate award");
        Reject(() => store.Settle(pending.Id, "Lost", 12, false), "conflicting result");
        Reject(() => store.Settle(pending.Id, "Won", 13, false), "conflicting turns");
        Reject(() => store.Settle("unknown", "Won", 12, false), "unknown match");
        Reject(() => store.Settle(pending.Id, "Oops", 12, false), "invalid result");
        Check(store.AcknowledgeRewardPresentation(pending.Id), "legacy reward presentation acknowledgement");
    }
    using (var store = new LocalProfile(freshPath))
    {
        Check(store.Gold == freshGold && store.Stars == LadderRules.Fresh().Multiplier && store.History.Count == 1, "restart persistence");
        var pending = store.Begin("warrior", "mage", 124);
        var loss = store.Settle(pending.Id, "Lost", 10, false);
        var lossReward = Rolled(pending, "Lost", 10);
        Check(loss.Gold == lossReward.Gold && loss.DustReward == lossReward.Dust && loss.Gold > 0 && !loss.PackReward
            && store.Gold == freshGold + loss.Gold && loss.ClassXpAfter > loss.ClassXpBefore, "a loss pays the rolled third and XP");
        freshGold = store.Gold;
        store.AcknowledgeRewardPresentation(pending.Id);
        pending = store.Begin("priest", "warrior", 125);
        Check(store.Settle(pending.Id, "Lost", 20, true).Gold == 0, "concede rewarded");
        store.AcknowledgeRewardPresentation(pending.Id);
        pending = store.Begin("mage", "priest", 126);
        Check(store.Settle(pending.Id, "Lost", 1, false).Gold == 0, "short loss rewarded");
        store.AcknowledgeRewardPresentation(pending.Id);
        store.Begin("mage", "warrior", 127);
    }
    using (var store = new LocalProfile(freshPath))
    {
        Check(store.Pending != null, "pending lost on restart");
        store.RecoverInterrupted();
        Check(store.Pending == null && store.PendingRewardPresentation == null
            && store.History.Last().Result == "Abandoned" && store.Gold == freshGold, "crash recovery");
        var match = store.Begin("mage", "priest", 128);
        int beforeRetryHistory = store.History.Count;
        int beforeRetryGold = store.Gold;
        var retryFacts = new LocalRewardFacts { DistinctCardsPlayed = 6, EnemyMinionsDefeated = 5, MinimumHealth = 5 };
        ReplaceFileWithDirectory(freshPath, out var saved);
        bool failed = false;
        try { store.Settle(match.Id, "Won", 12, false, retryFacts); } catch (IOException) { failed = true; }
        catch (UnauthorizedAccessException) { failed = true; }
        Check(failed && store.Pending.Id == match.Id && store.Gold == freshGold && store.PendingRewardPresentation == null,
            "failed save mutated state or invented reward receipt");
        Check(LocalRewardPresentation.CanServiceSettlementRetry(true, "Result", true),
            "failed settlement remains retryable in completed GAMEPLAY without a receipt");
        RestoreFile(freshPath, saved);
        LocalMatchRecord retried = store.Settle(match.Id, "Won", 12, false, retryFacts);
        Check(retried.Id == match.Id && store.Pending == null
            && store.PendingRewardPresentation?.Id == match.Id
            && store.History.Count == beforeRetryHistory + 1
            && store.Gold == beforeRetryGold + retried.Gold,
            "same-fact retry settled once with one durable reward receipt");
        Check(LocalRewardPresentation.ShouldRouteResultHome(true, true, "Result", true)
            && !LocalRewardPresentation.CanPresentInScene(true, false)
            && LocalRewardPresentation.CanPresentInScene(true, true)
            && LocalRewardPresentation.Summary(retried).Header == "VICTORY",
            "successful retry routes HUB before one reward summary");
        int afterRetryGold = store.Gold, afterRetryHistory = store.History.Count;
        Check(store.Settle(match.Id, "Won", 12, false, retryFacts).Id == match.Id
            && store.Gold == afterRetryGold && store.History.Count == afterRetryHistory,
            "same-fact retry replay cannot regrant or duplicate history");
        store.AcknowledgeRewardPresentation(match.Id);
    }

    string rewardReceiptPath = Path.Combine(root, "reward-receipt.xml");
    string rewardReceiptId; int rewardReceiptGold;
    using (var store = new LocalProfile(rewardReceiptPath))
    {
        var pending = store.Begin("mage", "priest", 811);
        var settled = store.Settle(pending.Id, "Won", 8, false,
            new LocalRewardFacts { DistinctCardsPlayed = 6, EnemyMinionsDefeated = 5, MinimumHealth = 5 });
        rewardReceiptId = settled.Id; rewardReceiptGold = settled.Gold;
        Check(store.PendingRewardPresentation.Id == settled.Id && store.Gold == settled.Gold
            && store.UnopenedPacks == 0, "settlement atomically records reward presentation receipt");
        var exposedReceipt = store.PendingRewardPresentation;
        exposedReceipt.Result = "Lost";
        Check(store.PendingRewardPresentation.Result == "Won", "reward presentation getter escaped by reference");
        byte[] settledBytes = File.ReadAllBytes(rewardReceiptPath);
        Check(store.Settle(pending.Id, "Won", 8, false,
            new LocalRewardFacts { DistinctCardsPlayed = 6, EnemyMinionsDefeated = 5, MinimumHealth = 5 }).Id == settled.Id
            && File.ReadAllBytes(rewardReceiptPath).SequenceEqual(settledBytes),
            "duplicate settlement regranted or rewrote pending reward receipt");
        Reject(() => store.Begin("mage", "warrior", 812), "new match while reward presentation is pending");
        Reject(() => store.AcknowledgeRewardPresentation("unknown"), "unknown reward acknowledgement");
        Reject(() => store.AcknowledgeRewardPresentation(Guid.NewGuid().ToString()), "stale reward acknowledgement");

        ReplaceFileWithDirectory(rewardReceiptPath, out var beforeAckFailure);
        bool failed = false;
        try { store.AcknowledgeRewardPresentation(settled.Id); } catch (IOException) { failed = true; }
        catch (UnauthorizedAccessException) { failed = true; }
        Check(failed && store.PendingRewardPresentation.Id == settled.Id,
            "failed reward acknowledgement changed memory");
        RestoreFile(rewardReceiptPath, beforeAckFailure);
        Check(store.AcknowledgeRewardPresentation(settled.Id), "reward acknowledgement retry did not commit");
        byte[] acknowledgedBytes = File.ReadAllBytes(rewardReceiptPath);
        Check(!store.AcknowledgeRewardPresentation(settled.Id)
            && File.ReadAllBytes(rewardReceiptPath).SequenceEqual(acknowledgedBytes),
            "duplicate reward acknowledgement rewrote profile");
        Check(store.Settle(pending.Id, "Won", 8, false,
            new LocalRewardFacts { DistinctCardsPlayed = 6, EnemyMinionsDefeated = 5, MinimumHealth = 5 }).Id == settled.Id
            && store.PendingRewardPresentation == null && store.Gold == settled.Gold && store.UnopenedPacks == 0,
            "settlement retry reopened acknowledged reward receipt");
    }
    using (var reopened = new LocalProfile(rewardReceiptPath))
    {
        Check(reopened.PendingRewardPresentation == null && reopened.Gold == rewardReceiptGold && reopened.UnopenedPacks == 0,
            "acknowledged reward receipt changed durable grant");
        Check(!reopened.AcknowledgeRewardPresentation(rewardReceiptId),
            "known completed reward acknowledgement was not idempotent");
    }

    string pendingRewardRestartPath = Path.Combine(root, "pending-reward-restart.xml");
    using (var store = new LocalProfile(pendingRewardRestartPath))
    {
        var pending = store.Begin("mage", "priest", 813);
        rewardReceiptGold = store.Settle(pending.Id, "Lost", 4, false,
            new LocalRewardFacts { DistinctCardsPlayed = 6, EnemyMinionsDefeated = 0, MinimumHealth = 30 }).Gold;
        rewardReceiptId = pending.Id;
    }
    byte[] pendingRewardBytes = File.ReadAllBytes(pendingRewardRestartPath);
    using (var reopened = new LocalProfile(pendingRewardRestartPath))
    {
        Check(reopened.PendingRewardPresentation.Id == rewardReceiptId && reopened.Gold == rewardReceiptGold && rewardReceiptGold > 0,
            "pending reward presentation did not survive restart");
        Check(reopened.AcknowledgeRewardPresentation(rewardReceiptId), "restart reward acknowledgement failed");
    }
    foreach (string forged in new[] { "unknown", "duplicate" })
    {
        string forgedPath = Path.Combine(root, "forged-reward-receipt-" + forged + ".xml");
        var invalid = ReadProfile(pendingRewardBytes);
        if (forged == "unknown") invalid.PendingRewardPresentationId = Guid.NewGuid().ToString();
        else invalid.Matches.Add(invalid.Matches[0]);
        WriteProfile(forgedPath, invalid);
        byte[] forgedBytes = File.ReadAllBytes(forgedPath);
        Reject(() => { using var ignored = new LocalProfile(forgedPath); }, "forged reward receipt " + forged);
        Check(File.ReadAllBytes(forgedPath).SequenceEqual(forgedBytes), "forged reward receipt was rewritten: " + forged);
    }
    string simultaneousPendingPath = Path.Combine(root, "forged-reward-receipt-with-different-pending.xml");
    var simultaneousPending = ReadProfile(pendingRewardBytes);
    simultaneousPending.Pending = new LocalMatchRecord {
        Id = Guid.NewGuid().ToString(), Deck = "mage", Opponent = "warrior", Seed = 814,
        StartedUtc = DateTimeOffset.UtcNow.ToString("O"), RewardVersion = LocalEconomy.RollRewardVersion, RewardFacts = new LocalRewardFacts()
    };
    Check(simultaneousPending.Pending.Id != simultaneousPending.PendingRewardPresentationId,
        "simultaneous pending fixture must use distinct transaction ids");
    // Control: the same pending match without the reward receipt loads, so the rejection below is about the receipt.
    string pendingControlPath = Path.Combine(root, "pending-without-reward-receipt.xml");
    var pendingControl = ReadProfile(pendingRewardBytes);
    pendingControl.Pending = simultaneousPending.Pending; pendingControl.PendingRewardPresentationId = null;
    WriteProfile(pendingControlPath, pendingControl);
    using (var control = new LocalProfile(pendingControlPath)) Check(control.Pending.Id == simultaneousPending.Pending.Id, "control: a valid pending match loads");
    WriteProfile(simultaneousPendingPath, simultaneousPending);
    byte[] simultaneousPendingBytes = File.ReadAllBytes(simultaneousPendingPath);
    Reject(() => { using var ignored = new LocalProfile(simultaneousPendingPath); },
        "reward receipt alongside a different pending match");
    Check(File.ReadAllBytes(simultaneousPendingPath).SequenceEqual(simultaneousPendingBytes),
        "rejected simultaneous pending match and reward receipt was rewritten");
    string abandonedRewardPath = Path.Combine(root, "forged-reward-receipt-abandoned.xml");
    var abandonedReward = ReadProfile(File.ReadAllBytes(freshPath));
    abandonedReward.PendingRewardPresentationId = abandonedReward.Matches.Single(match => match.Result == "Abandoned").Id;
    WriteProfile(abandonedRewardPath, abandonedReward);
    byte[] abandonedRewardBytes = File.ReadAllBytes(abandonedRewardPath);
    Reject(() => { using var ignored = new LocalProfile(abandonedRewardPath); }, "abandoned reward receipt");
    Check(File.ReadAllBytes(abandonedRewardPath).SequenceEqual(abandonedRewardBytes), "abandoned reward receipt was rewritten");

    string rewardsPath = Path.Combine(root, "rewards.xml");
    using (var store = new LocalProfile(rewardsPath))
    {
        var match = store.Begin("mage", "priest", 1);
        var facts = new LocalRewardFacts { DistinctCardsPlayed = 6, EnemyMinionsDefeated = 4, MinimumHealth = 30 };
        Reject(() => store.Settle(match.Id, "Lost", int.MaxValue, false, facts), "unbounded turn count");
        var reward = store.Settle(match.Id, "Lost", 4, false, facts);
        facts.DistinctCardsPlayed = 0;
        var lossExpected = Rolled(match, "Lost", 4);
        Check(reward.Gold == lossExpected.Gold && reward.DustReward == lossExpected.Dust && reward.Gold > 0 && reward.BonusGold == 0 && !reward.PackReward,
            "a loss pays only the rolled third");
        Check(store.Settle(match.Id, "Lost", 4, false,
            new LocalRewardFacts { DistinctCardsPlayed = 6, EnemyMinionsDefeated = 4, MinimumHealth = 30 }).Gold == reward.Gold,
            "identical factual retry");
        Reject(() => store.Settle(match.Id, "Lost", 4, false,
            new LocalRewardFacts { DistinctCardsPlayed = 5, EnemyMinionsDefeated = 4, MinimumHealth = 30 }), "conflicting reward facts");
        store.AcknowledgeRewardPresentation(match.Id);

        // Rolled (V6) rewards ignore the facts: neither the enemy-minion nor the great-win facts add bonus gold or a pack.
        match = store.Begin("mage", "priest", 2);
        var minionFacts = new LocalRewardFacts { DistinctCardsPlayed = 0, EnemyMinionsDefeated = 5, MinimumHealth = 30 };
        reward = store.Settle(match.Id, "Won", 4, false, minionFacts);
        var expected = Rolled(match, "Won", 4);
        Check(reward.BonusGold == 0 && !reward.PackReward && reward.Gold == expected.Gold && reward.DustReward == expected.Dust,
            "enemy-minion facts do not change a rolled reward");
        store.AcknowledgeRewardPresentation(match.Id);

        match = store.Begin("mage", "priest", 3);
        var greatFacts = new LocalRewardFacts { DistinctCardsPlayed = 6, EnemyMinionsDefeated = 5, MinimumHealth = 5 };
        reward = store.Settle(match.Id, "Won", 4, false, greatFacts);
        expected = Rolled(match, "Won", 4);
        Check(reward.Gold == expected.Gold && reward.DustReward == expected.Dust && reward.BonusGold == 0 && !reward.PackReward
            && store.UnopenedPacks == 0, "great-win facts do not add a pack to a rolled reward");
        store.AcknowledgeRewardPresentation(match.Id);

        match = store.Begin("mage", "priest", 4);
        reward = store.Settle(match.Id, "Won", 3, false,
            new LocalRewardFacts { DistinctCardsPlayed = 99, EnemyMinionsDefeated = 99, MinimumHealth = 1 });
        Check(reward.Gold == 0 && reward.DustReward == 0 && reward.BonusGold == 0 && !reward.PackReward, "short match reward gating");
    }

    string packPath = Path.Combine(root, "packs.xml");
    GoldProfile(packPath, 175);
    string buyRetryPath = Path.Combine(root, "buy-retry.xml");
    GoldProfile(buyRetryPath, 175);
    using (var store = new LocalProfile(buyRetryPath))
    {
        var beforeCollection = store.Collection.ToDictionary(pair => pair.Key, pair => pair.Value);
        ReplaceFileWithDirectory(buyRetryPath, out var saved);
        bool failed = false;
        try { store.BuyPack(); } catch (IOException) { failed = true; }
        catch (UnauthorizedAccessException) { failed = true; }
        Check(failed && store.Gold == 175 && store.UnopenedPacks == 0 &&
            store.Collection.OrderBy(pair => pair.Key).SequenceEqual(beforeCollection.OrderBy(pair => pair.Key)),
            "failed purchase mutated state");
        ((Dictionary<string, int>)store.Collection).Clear();
        Check(store.Collection.Count == beforeCollection.Count, "failed purchase collection escaped by reference");
        RestoreFile(buyRetryPath, saved);
        Check(File.ReadAllBytes(buyRetryPath).SequenceEqual(saved), "failed purchase changed disk profile");
        store.BuyPack();
        Check(store.Gold == 75 && store.UnopenedPacks == 1, "purchase retry did not commit exactly once");
    }
    using (var store = new LocalProfile(buyRetryPath))
        Check(store.Gold == 75 && store.UnopenedPacks == 1, "purchase retry did not persist");

    using (var store = new LocalProfile(packPath))
    {
        store.BuyPack();
        Check(store.Gold == 75 && store.UnopenedPacks == 1, "pack purchase");
        byte[] beforeOpen = File.ReadAllBytes(packPath);
        string controlPath = Path.Combine(root, "pack-control.xml");
        File.WriteAllBytes(controlPath, beforeOpen);
        ReplaceFileWithDirectory(packPath, out var saved);
        bool failed = false;
        try { store.OpenPack(); } catch (IOException) { failed = true; }
        catch (UnauthorizedAccessException) { failed = true; }
        Check(failed && store.UnopenedPacks == 1 && store.LastPack == null, "failed pack save mutated state");
        RestoreFile(packPath, saved);
        var opened = store.OpenPack();
        Check(opened.Cards.Count == 5 && store.UnopenedPacks == 0, "pack size or debit");
        Check(new[] { Rarity.RARE, Rarity.EPIC, Rarity.LEGENDARY }.Contains(
            SabberStoneCore.Model.Cards.FromId(opened.Cards[0].Id).Rarity), "first pack slot quality");
        Check(store.Collection.All(p => p.Value <= (SabberStoneCore.Model.Cards.FromId(p.Key).Rarity == Rarity.LEGENDARY ? 1 : 2)),
            "collection exceeded playable cap");
        using var control = new LocalProfile(controlPath);
        var expected = control.OpenPack();
        Check(opened.Cards.Select(c => c.Id + ":" + c.DuplicateGold).SequenceEqual(
            expected.Cards.Select(c => c.Id + ":" + c.DuplicateGold)), "failed-save retry rerolled pack");
        Check(!store.PackRevealPending && !control.PackRevealPending, "legacy OpenPack created native pending state");
    }
    using (var store = new LocalProfile(packPath))
    {
        Check(store.LastPack != null && store.LastPack.Cards.Count == 5 && store.UnopenedPacks == 0, "pack receipt persistence");
        var exposed = store.LastPack;
        exposed.Cards.Clear();
        Check(store.LastPack.Cards.Count == 5, "pack receipt escaped by reference");
    }

    string poorPath = Path.Combine(root, "poor.xml");
    using (var poor = new LocalProfile(poorPath))
    {
        Check(!poor.PackPoolComplete, "fresh pack pool reported complete");
        Reject(() => poor.BuyPack(), "insufficient gold");
        Reject(() => poor.BeginNativePack(), "native open with zero packs");
        Check(poor.Gold == 0 && poor.UnopenedPacks == 0 && !poor.PackRevealPending && poor.LastPack == null,
            "failed purchase or zero-pack native open mutated state");
    }

    string[] expectedHeroes = LocalCoreProgression.Heroes.ToArray();
    Check(LocalDecks.PlayerHeroes.SequenceEqual(expectedHeroes), "player hero authority matches running engine");
    var neutralDeck = LocalCardCatalog.Supported.Where(card => card.Class == SabberStoneCore.Enums.CardClass.NEUTRAL)
        .GroupBy(card => LocalCardCatalog.CopyGroup(card.Id)).Select(group => group.First())
        .SelectMany(card => Enumerable.Repeat(card.Id, card.Rarity == SabberStoneCore.Enums.Rarity.LEGENDARY ? 1 : 2)).Take(30).ToArray();
    foreach (string hero in expectedHeroes)
    {
        Check(LocalDecks.Custom("Every class", hero, neutralDeck).Build().Count == 30
            && LocalDecks.Draft("Every draft", hero, neutralDeck.Take(1)).Hero == hero, "custom and draft accept " + hero);
        var classCard = LocalCardCatalog.Supported.FirstOrDefault(card => card.Class == SabberStoneCore.Model.Cards.FromId(hero).Class);
        if (classCard != null)
            Check(LocalDecks.Draft("Class card", hero, new[] { classCard.Id }).CardIds.Count == 1, "class card accepted " + hero);
        Reject(() => LocalDecks.Draft("Off class", hero, new[] { hero == "HERO_08" ? "CS2_106" : "CS2_029" }), "off-class card " + hero);
    }
    Reject(() => LocalDecks.Custom("Invalid hero", "HERO_99", neutralDeck), "invalid hero custom");
    Reject(() => LocalDecks.Draft("Invalid hero", "HERO_99", Array.Empty<string>()), "invalid hero draft");
    var mageCards = LocalDecks.Get("mage").CardIds;
    var mutable = mageCards.ToList();
    var custom = LocalDecks.Custom("Arcane", "HERO_08", mutable);
    mutable[0] = "bad";
    var leaked = custom.CardIds;
    leaked[0] = "bad";
    Check(custom.Id == "custom" && custom.CardIds[0] != "bad" && custom.Build().Count == 30, "custom deck mutability");
    Reject(() => LocalDecks.Custom("Arcane", "HERO_08", mageCards.Take(29)), "short deck");
    var wrongClass = mageCards.ToList(); wrongClass[0] = "CS2_106";
    Reject(() => LocalDecks.Custom("Arcane", "HERO_08", wrongClass), "wrong-class card");
    var twoLegendaries = mageCards.ToList(); twoLegendaries[0] = twoLegendaries[1] = "EX1_561";
    Reject(() => LocalDecks.Custom("Arcane", "HERO_08", twoLegendaries), "two legendary copies");
    Reject(() => LocalDecks.Custom("\u200B", "HERO_08", mageCards), "invisible-only deck name");
    Reject(() => LocalDecks.Custom("Arcane\u2028Mage", "HERO_08", mageCards), "line-separator deck name");
    Reject(() => LocalDecks.Custom("Arcane\u2029Mage", "HERO_08", mageCards), "paragraph-separator deck name");
    Check(LocalDecks.Custom("👩‍🚀", "HERO_08", mageCards).Build().Count == 30, "valid emoji deck name");
    Check(LocalDecks.Draft("Empty Mage", "HERO_08", Array.Empty<string>()).CardIds.Count == 0,
        "empty draft rejected");
    Check(LocalDecks.Draft("Partial Mage", "HERO_08", mageCards.Take(29)).CardIds.Count == 29,
        "incomplete draft rejected");
    Reject(() => LocalDecks.Draft("Too Large", "HERO_08", mageCards.Concat(new[] { mageCards[0] })),
        "draft over 30 cards");

    string libraryPath = Path.Combine(root, "deck-library.xml");
    long firstId, secondId;
    using (var store = new LocalProfile(libraryPath))
    {
        Check(store.CustomDecks.Count == 0 && store.NextCustomDeckId == 910000010L,
            "fresh profile deck library defaults");
        var first = store.CreateDraft("First Mage", "HERO_08");
        var second = store.CreateDraft("Second Mage", "HERO_08");
        firstId = first.Id; secondId = second.Id;
        Check(firstId == 910000010L && secondId == 910000011L && store.NextCustomDeckId == 910000012L,
            "draft allocator sequence");
        store.SaveDraft(firstId, "Three Cards", "HERO_08", mageCards.Take(3), 40);
        store.SaveDraft(secondId, "Full Deck", "HERO_08", mageCards, 7);
        Check(store.GetDraft(firstId).Cards.SequenceEqual(mageCards.Take(3)) && store.GetDraft(firstId).SortOrder == 40
            && store.GetDraft(secondId).Cards.SequenceEqual(mageCards) && store.GetDraft(secondId).SortOrder == 7,
            "drafts save independently with real counts and order");
        var exposed = store.GetDraft(firstId); exposed.Name = "mutated"; exposed.Cards.Clear();
        var exposedList = store.CustomDecks; exposedList[1].Cards.Clear();
        Check(store.GetDraft(firstId).Name == "Three Cards" && store.GetDraft(firstId).Cards.Count == 3
            && store.GetDraft(secondId).Cards.Count == 30, "draft getters escaped by reference");

        byte[] beforeInvalid = File.ReadAllBytes(libraryPath);
        Reject(() => store.SaveDraft(firstId, "", "HERO_08", mageCards.Take(3), 1), "empty draft name");
        Reject(() => store.SaveDraft(firstId, new string('x', 33), "HERO_08", mageCards.Take(3), 1), "long draft name");
        Reject(() => store.SaveDraft(firstId, "Bad Hero", "HERO_99", mageCards.Take(3), 1), "unsupported draft hero");
        Reject(() => store.SaveDraft(firstId, "Wrong Class", "HERO_08", new[] { "CS2_106" }, 1), "wrong class draft");
        Reject(() => store.SaveDraft(firstId, "Unknown", "HERO_08", new[] { "BAD" }, 1), "unknown draft card");
        Reject(() => store.SaveDraft(firstId, "Copies", "HERO_08", new[] { mageCards[0], mageCards[0], mageCards[0] }, 1), "third copy");
        Reject(() => store.SaveDraft(firstId, "Legendary", "HERO_08", new[] { "EX1_561", "EX1_561" }, 1), "second legendary");
        store.SaveDraft(firstId, "Unowned", "HERO_08", LocalDecks.Get("mage-frost").CardIds.Take(3), 1);
        Check(!store.IsDeckComplete(firstId), "unowned draft remains incomplete");
        store.SaveDraft(firstId, "Three Cards", "HERO_08", mageCards.Take(3), 40);
        Reject(() => store.SaveDraft(firstId, "Too Large", "HERO_08", mageCards.Concat(new[] { mageCards[0] }), 1), "31 cards");
        Reject(() => store.SaveDraft(999999999L, "Missing", "HERO_08", Array.Empty<string>(), 1), "missing draft ID");
        Reject(() => store.SaveDraft(firstId, "Order", "HERO_08", Array.Empty<string>(), -1), "negative sort order");
        Check(File.ReadAllBytes(libraryPath).SequenceEqual(beforeInvalid) && store.GetDraft(firstId).Name == "Three Cards",
            "invalid draft input changed bytes or memory");

        ReplaceFileWithDirectory(libraryPath, out var beforeCreateFailure);
        long nextBeforeFailure = store.NextCustomDeckId;
        bool createFailed = false;
        try { store.CreateDraft("Create Failure", "HERO_08"); } catch (IOException) { createFailed = true; }
        catch (UnauthorizedAccessException) { createFailed = true; }
        Check(createFailed && store.NextCustomDeckId == nextBeforeFailure && store.CustomDecks.Count == 2,
            "failed create mutated library or allocator");
        RestoreFile(libraryPath, beforeCreateFailure);

        ReplaceFileWithDirectory(libraryPath, out var beforeSaveFailure);
        bool saveFailed = false;
        try { store.SaveDraft(firstId, "Save Failure", "HERO_08", mageCards.Take(4), 3); } catch (IOException) { saveFailed = true; }
        catch (UnauthorizedAccessException) { saveFailed = true; }
        Check(saveFailed && store.GetDraft(firstId).Name == "Three Cards" && store.GetDraft(firstId).Cards.Count == 3,
            "failed draft save mutated memory");
        RestoreFile(libraryPath, beforeSaveFailure);

        ReplaceFileWithDirectory(libraryPath, out var beforeDeleteFailure);
        bool deleteFailed = false;
        try { store.DeleteDraft(firstId); } catch (IOException) { deleteFailed = true; }
        catch (UnauthorizedAccessException) { deleteFailed = true; }
        Check(deleteFailed && store.GetDraft(firstId) != null, "failed delete mutated memory");
        RestoreFile(libraryPath, beforeDeleteFailure);

        store.DeleteDraft(firstId);
        Reject(() => store.DeleteDraft(firstId), "duplicate delete");
        Check(store.GetDraft(firstId) == null && store.NextCustomDeckId == 910000012L,
            "delete rewound allocator or retained draft");
    }
    using (var store = new LocalProfile(libraryPath))
    {
        Check(store.GetDraft(firstId) == null && store.GetDraft(secondId).Name == "Full Deck"
            && store.GetDraft(secondId).Cards.SequenceEqual(mageCards) && store.GetDraft(secondId).SortOrder == 7,
            "deck library restart changed surviving draft");
        var replacement = store.CreateDraft("After Delete", "HERO_08");
        Check(replacement.Id == 910000012L && replacement.Id != firstId,
            "deleted draft ID was reused");
    }

    string reorderPath = Path.Combine(root, "deck-reorder.xml");
    long reorderFirst, reorderSecond, reorderThird;
    using (var store = new LocalProfile(reorderPath))
    {
        reorderFirst = store.CreateDraft("First", "HERO_08").Id;
        reorderSecond = store.CreateDraft("Second", "HERO_08").Id;
        reorderThird = store.CreateDraft("Third", "HERO_08").Id;
        store.SaveDraft(reorderFirst, "First", "HERO_08", Array.Empty<string>(), 20);
        store.SaveDraft(reorderSecond, "Second", "HERO_08", Array.Empty<string>(), 5);
        store.SaveDraft(reorderThird, "Third", "HERO_08", Array.Empty<string>(), 5);
        store.ReorderDrafts(new[] { reorderThird, reorderFirst, reorderSecond });
        Check(store.GetDraft(reorderThird).SortOrder == 0 && store.GetDraft(reorderFirst).SortOrder == 1
            && store.GetDraft(reorderSecond).SortOrder == 2, "batch reorder normalizes exact personal order");

        byte[] beforeInvalidReorder = File.ReadAllBytes(reorderPath);
        foreach (var invalidOrder in new[] {
            new[] { reorderThird, reorderFirst },
            new[] { reorderThird, reorderFirst, reorderFirst },
            new[] { reorderThird, reorderFirst, 999999999L }
        }) Reject(() => store.ReorderDrafts(invalidOrder), "invalid batch reorder permutation");
        Check(File.ReadAllBytes(reorderPath).SequenceEqual(beforeInvalidReorder)
            && store.GetDraft(reorderThird).SortOrder == 0 && store.GetDraft(reorderFirst).SortOrder == 1
            && store.GetDraft(reorderSecond).SortOrder == 2, "invalid reorder changed bytes or memory");

        ReplaceFileWithDirectory(reorderPath, out var beforeReorderFailure);
        bool reorderFailed = false;
        try { store.ReorderDrafts(new[] { reorderSecond, reorderFirst, reorderThird }); }
        catch (IOException) { reorderFailed = true; }
        catch (UnauthorizedAccessException) { reorderFailed = true; }
        Check(reorderFailed && store.GetDraft(reorderThird).SortOrder == 0
            && store.GetDraft(reorderFirst).SortOrder == 1 && store.GetDraft(reorderSecond).SortOrder == 2,
            "failed batch reorder changed memory");
        RestoreFile(reorderPath, beforeReorderFailure);
        Check(File.ReadAllBytes(reorderPath).SequenceEqual(beforeReorderFailure), "failed batch reorder changed profile bytes");
        store.ReorderDrafts(new[] { reorderSecond, reorderFirst, reorderThird });
    }
    using (var store = new LocalProfile(reorderPath))
        Check(store.GetDraft(reorderSecond).SortOrder == 0 && store.GetDraft(reorderFirst).SortOrder == 1
            && store.GetDraft(reorderThird).SortOrder == 2, "batch reorder retry survives restart");

    string capPath = Path.Combine(root, "deck-cap.xml");
    using (var store = new LocalProfile(capPath))
    {
        for (int i = 0; i < LocalProfile.MaximumCustomDecks; i++) store.CreateDraft("Deck " + i, "HERO_08");
        byte[] atCap = File.ReadAllBytes(capPath);
        long allocatorAtCap = store.NextCustomDeckId;
        Reject(() => store.CreateDraft("Overflow", "HERO_08"), "personal deck past the cap");
        Check(store.CustomDecks.Count == LocalProfile.MaximumCustomDecks && store.NextCustomDeckId == allocatorAtCap
            && File.ReadAllBytes(capPath).SequenceEqual(atCap), "deck cap rejection changed state");
    }

    var validLibrary = ReadProfile(File.ReadAllBytes(libraryPath));
    foreach (string forged in new[] { "duplicate", "reserved", "rewind", "overflow", "legacy-field" })
    {
        string forgedPath = Path.Combine(root, "forged-v4-" + forged + ".xml");
        var invalid = ReadProfile(File.ReadAllBytes(libraryPath));
        if (forged == "duplicate") invalid.CustomDecks.Add(new LocalCustomDeck {
            Id = invalid.CustomDecks[0].Id, SortOrder = 3, Name = "Duplicate", Hero = "HERO_08", Cards = new List<string>() });
        else if (forged == "reserved") invalid.CustomDecks[0].Id = 910000002L;
        else if (forged == "rewind") invalid.NextCustomDeckId = invalid.CustomDecks.Max(deck => deck.Id);
        else if (forged == "overflow") invalid.NextCustomDeckId = long.MaxValue;
        else invalid.CustomDeck = new LocalCustomDeck { Name = "Old Slot", Hero = "HERO_08", Cards = mageCards.ToList() };
        WriteProfile(forgedPath, invalid);
        byte[] forgedBytes = File.ReadAllBytes(forgedPath);
        Reject(() => { using var ignored = new LocalProfile(forgedPath); }, "forged V4 " + forged);
        Check(File.ReadAllBytes(forgedPath).SequenceEqual(forgedBytes), "forged V4 was rewritten: " + forged);
    }

    string exhaustedPath = Path.Combine(root, "exhausted-v4.xml");
    var exhausted = ReadProfile(File.ReadAllBytes(libraryPath));
    exhausted.NextCustomDeckId = long.MaxValue - 1;
    WriteProfile(exhaustedPath, exhausted);
    using (var store = new LocalProfile(exhaustedPath))
    {
        byte[] exhaustedBytes = File.ReadAllBytes(exhaustedPath);
        int exhaustedCount = store.CustomDecks.Count;
        Reject(() => store.CreateDraft("No ID", "HERO_08"), "exhausted allocator create");
        Check(store.NextCustomDeckId == long.MaxValue - 1 && store.CustomDecks.Count == exhaustedCount
            && File.ReadAllBytes(exhaustedPath).SequenceEqual(exhaustedBytes),
            "exhausted allocator rejection changed memory or bytes");
    }

    string deckPath = Path.Combine(root, "deck.xml");
    using (var store = new LocalProfile(deckPath))
    {
        var input = mageCards.ToList();
        store.SaveDeck("🔥 Mage", "HERO_08", input);
        input[0] = "bad";
        var saved = store.CustomDeck;
        saved.Cards[0] = "bad";
        Check(store.CustomDeck.Cards[0] != "bad" && store.ResolveCustomDeck().Build().Count == 30, "saved deck escaped by reference");
        store.SaveDeck("Frost", "HERO_08", LocalDecks.Get("mage-frost").CardIds);
        Check(!store.IsDeckComplete(store.CustomDeck.Id), "unowned deck saved incomplete");
        Reject(() => store.Begin("custom", "priest", 1), "incomplete deck match");
        store.SaveDeck("🔥 Mage", "HERO_08", mageCards);
        Reject(() => store.SaveDeck(new string('x', 33), "HERO_08", mageCards), "long name");

        var replacement = mageCards.AsEnumerable().Reverse().ToList();
        var beforeCollection = store.Collection.ToDictionary(pair => pair.Key, pair => pair.Value);
        ReplaceFileWithDirectory(deckPath, out var originalDeckBytes);
        bool failed = false;
        try { store.SaveDeck("Arcane Retry", "HERO_08", replacement); } catch (IOException) { failed = true; }
        catch (UnauthorizedAccessException) { failed = true; }
        Check(failed && store.CustomDeck.Name == "🔥 Mage" && store.CustomDeck.Hero == "HERO_08" &&
            store.CustomDeck.Cards.SequenceEqual(mageCards) && replacement.SequenceEqual(mageCards.AsEnumerable().Reverse()) &&
            store.Collection.OrderBy(pair => pair.Key).SequenceEqual(beforeCollection.OrderBy(pair => pair.Key)),
            "failed replacement save mutated profile or caller draft");
        var exposedAfterFailure = store.CustomDeck;
        exposedAfterFailure.Name = "bad";
        exposedAfterFailure.Cards.Clear();
        Check(store.CustomDeck.Name == "🔥 Mage" && store.CustomDeck.Cards.SequenceEqual(mageCards),
            "failed replacement deck escaped by reference");
        RestoreFile(deckPath, originalDeckBytes);
        Check(File.ReadAllBytes(deckPath).SequenceEqual(originalDeckBytes), "failed replacement changed disk profile");
        store.SaveDeck("Arcane Retry", "HERO_08", replacement);
        Check(store.CustomDeck.Name == "Arcane Retry" && store.CustomDeck.Cards.SequenceEqual(replacement),
            "replacement retry did not commit once");
    }
    using (var store = new LocalProfile(deckPath))
    {
        var replacement = mageCards.AsEnumerable().Reverse().ToList();
        Check(store.CustomDeck.Name == "Arcane Retry" && store.CustomDeck.Hero == "HERO_08" &&
            store.ResolveCustomDeck().CardIds.SequenceEqual(replacement), "replacement retry restart");
        var ui = new LocalCollectionUI(store);
        Check(ui.DraftValid && ui.DraftCount == 30 && ui.DraftName == "Arcane Retry", "saved custom did not seed editor");
        var heroName = typeof(LocalCollectionUI).GetMethod("HeroName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var filter = typeof(LocalCollectionUI).GetField("collectionFilter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var filteredCards = typeof(LocalCollectionUI).GetMethod("CollectionCards", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        foreach (string hero in LocalDecks.PlayerHeroes)
        {
            string name = (string)heroName.Invoke(null, new object[] { hero });
            Check(name == LocalCoreProgression.ClassName(hero) && name != "Unknown", "collection class name " + hero);
            filter.SetValue(ui, name);
            var actual = (List<SabberStoneCore.Model.Card>)filteredCards.Invoke(ui, null);
            var heroClass = SabberStoneCore.Model.Cards.FromId(hero).Class;
            Check(actual.Select(card => card.Id).ToHashSet().SetEquals(LocalDecks.CustomCards
                .Where(card => card.Class == heroClass || card.Class == SabberStoneCore.Enums.CardClass.NEUTRAL).Select(card => card.Id)),
                "collection filter uses engine class " + hero);
        }
        ui.SelectCustom();
        Check(ui.SelectedDeck == "custom:" + store.CustomDeck.Id, "compatibility editor did not bind explicit personal ID");
        var selectedCustom = ui.ResolveSelectedDeck();
        var selectedCards = selectedCustom.CardIds;
        selectedCards[0] = "bad";
        Check(selectedCustom.Name == "Arcane Retry" && selectedCustom.CardIds.SequenceEqual(replacement),
            "custom start selection was not resolved as a snapshot");
    }

    string ambiguousUiPath = Path.Combine(root, "ui-ambiguous.xml");
    using (var store = new LocalProfile(ambiguousUiPath))
    {
        store.CreateDraft("One", "HERO_08");
        store.CreateDraft("Two", "HERO_08");
        byte[] beforeAmbiguousSave = File.ReadAllBytes(ambiguousUiPath);
        var ui = new LocalCollectionUI(store);
        Check(ui.Notice.Contains("Multiple personal decks"), "compatibility editor does not state library ambiguity");
        // Multiple saved decks are the normal state after buying a ready-made deck: ladder-state must stay error-free.
        Check(ui.ActionError == "", "multiple saved decks reported as a ladder error");
        Reject(ui.SelectCustom, "compatibility editor selected an ambiguous personal deck");
        Reject(ui.SaveCompatibilityDraft, "compatibility editor overwrote an arbitrary personal deck");
        Check(File.ReadAllBytes(ambiguousUiPath).SequenceEqual(beforeAmbiguousSave),
            "ambiguous compatibility save changed profile bytes");

        // The 3-second status routine reads non-copying summaries; they must agree with the copying accessors.
        var summaries = store.VisibleDeckSummaries();
        Check(summaries.Count == 2 && summaries.Select(deck => deck.Id).SequenceEqual(store.CustomDecks.Select(deck => deck.Id))
            && summaries.Select(deck => deck.Name).SequenceEqual(store.CustomDecks.Select(deck => deck.Name))
            && summaries.Select(deck => deck.CardCount).SequenceEqual(store.CustomDecks.Select(deck => deck.Cards.Count)),
            "status deck summaries differ from CustomDecks");
        store.OwnedCollectionTotals(out int statusDistinct, out int statusTotal);
        Check(statusDistinct == store.Collection.Count && statusTotal == store.Collection.Values.Sum(), "status collection totals differ from Collection");
        Check(store.MatchCount == store.History.Count && store.PendingMatchId == store.Pending?.Id, "status match counts differ from History/Pending");
        string identityBefore = store.SavedFileIdentity(out _);
        Check(identityBefore != null, "saved profile has an identity");
        store.CreateDraft("Three", "HERO_08");
        Check(store.SavedFileIdentity(out _) != identityBefore, "a commit changes the saved file identity");
    }

    string uiFreshPath = Path.Combine(root, "ui-fresh.xml");
    using (var store = new LocalProfile(uiFreshPath))
    {
        var ui = new LocalCollectionUI(store);
        Check(ui.DraftName == "My Mage Deck" && ui.DraftCount == 30 && ui.DraftValid,
            "fresh mouse-only draft is not an owned saveable starter");
        Check(ui.ResolveSelectedDeck().Id == "mage", "default selected loaner changed");

        ui.SelectLoaner("paladin");
        Check(ui.ResolveSelectedDeck().Id == "paladin", "Paladin menu selection did not resolve the value loaner");
        ui.LoadLoaner(6);
        Check(ui.DraftName == "My Paladin Deck" && ui.DraftHero == "HERO_04" && ui.DraftCount == 30 && ui.DraftValid &&
            ui.DraftCardIds.Split(',').OrderBy(id => id, StringComparer.Ordinal)
                .SequenceEqual(LocalDecks.Get("paladin").CardIds.OrderBy(id => id, StringComparer.Ordinal)),
            "fresh profile could not load the owned Paladin value loaner");

        ui.LoadLoaner(0);
        ui.SelectLoaner("paladin");
        ui.ResetDraft();
        Check(ui.DraftName == "My Paladin Deck" && ui.DraftHero == "HERO_04" && ui.DraftCount == 30 && ui.DraftValid &&
            ui.DraftCardIds.Split(',').OrderBy(id => id, StringComparer.Ordinal)
                .SequenceEqual(LocalDecks.Get("paladin").CardIds.OrderBy(id => id, StringComparer.Ordinal)),
            "fresh profile reset did not restore the selected owned Paladin loaner");
    }

    Check(LocalProfile.NextStars(0, "Lost") == 0, "Bronze loss");
    Check(LocalProfile.NextStars(29, "Lost") == 29, "Bronze star loss");
    Check(LocalProfile.NextStars(30, "Lost") == 30, "Silver floor");
    Check(LocalProfile.NextStars(34, "Lost") == 33, "rank loss");
    Check(LocalProfile.NextStars(135, "Lost") == 135, "Diamond 5 floor");
    Check(LocalProfile.NextStars(149, "Won") == 150 && LocalProfile.RankName(150) == "Legend", "Legend promotion");
    Check(LocalProfile.NextStars(150, "Lost") == 150, "Legend floor");

    string missingVersionPath = Path.Combine(root, "missing-version.xml");
    string missingVersion = File.ReadAllText(freshPath).Replace("<Version>15</Version>", "");
    Check(!missingVersion.Contains("<Version>"), "missing-version fixture still has a version");
    File.WriteAllText(missingVersionPath, missingVersion);
    Reject(() => { using var ignored = new LocalProfile(missingVersionPath); }, "missing profile version");
    Check(File.ReadAllText(missingVersionPath) == missingVersion, "missing-version profile was modified");

    string corruptPath = Path.Combine(root, "corrupt.xml");
    File.WriteAllText(corruptPath, "<broken>");
    bool corrupt = false;
    try { using var ignored = new LocalProfile(corruptPath); } catch (InvalidOperationException) { corrupt = true; }
    Check(corrupt && File.ReadAllText(corruptPath) == "<broken>", "corrupt profile silently reset");
    Console.WriteLine("PASS local economy: atomic wallet/packs, rewards, collection, custom decks");
}
finally { Directory.Delete(root, true); }
