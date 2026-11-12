using System.Xml.Serialization;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

internal static class PremiumPackChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception("Premium packs: " + message); }
    private static void Reject(Action action, string message)
    {
        try { action(); } catch (Exception error) when (error is InvalidOperationException || error is IOException) { return; }
        throw new Exception("Expected rejection: " + message);
    }
    private static LocalProfileData Read(string path) { using var stream = File.OpenRead(path); return (LocalProfileData)new XmlSerializer(typeof(LocalProfileData)).Deserialize(stream); }
    private static void Write(string path, LocalProfileData data) { using var stream = File.Create(path); new XmlSerializer(typeof(LocalProfileData)).Serialize(stream, data); }
    private static readonly FairPackCounters Before = new FairPackCounters { PacksOpened = 9, PacksSinceLegendary = 9, PacksSinceEpic = 9 };
    private static void Add(LocalProfileData data, string id, int premium, int count)
    {
        var rows = premium == 0 ? data.Collection : data.PremiumCollection;
        var card = rows.SingleOrDefault(row => row.Id == id && row.Premium == premium);
        if (card == null) { card = new LocalCardCount { Id = id, Premium = premium }; rows.Add(card); }
        card.Count += count; card.Acquired = Math.Min(premium == 0 ? LocalCrafting.Playset(id) : 2, card.Acquired + count);
    }
    internal static void Run(string root)
    {
        string dir = Path.Combine(root, "premium-packs"); Directory.CreateDirectory(dir);
        string seedPath = Path.Combine(dir, "seed.xml");
        const int seed = 74;
        var fixture = FairPackRules.Generate(FairPackRules.Standard, 2, seed, 9, Before, _ => 0);
        Check(string.Join(",", fixture.CardIds.Select((id, slot) => LocalPackCard.ReceiptKey(id, fixture.Premiums[slot])))
            == "JAIL_831:3,TIME_860:1,EDR_110,TLC_835,EDR_477", "Golden/Signature rule-2 corpus");
        using (var profile = new LocalProfile(seedPath)) profile.CommitChange(next => {
            next.PackSeed = seed; next.PackSequence = 9;
            next.PackCounters.Single(row => row.PackType == FairPackRules.Standard).Counters = Before.Copy();
            next.UnopenedPacks.Add(new LocalPackCount { PackType = FairPackRules.Standard, Count = 40 });
            return true;
        });
        string Copy(string name) { string path = Path.Combine(dir, name + ".xml"); File.Copy(seedPath, path); return path; }
        Single(Copy("single"));
        foreach (bool mass in new[] { false, true }) foreach (string stage in new[] { "write", "flush", "replace" }) {
            string path = Copy("fault-" + mass + "-" + stage);
            using var profile = new LocalProfile(path);
            byte[] bytes = File.ReadAllBytes(path); string owned = string.Join(",", profile.OwnedCards.Select(row => row.Id + ":" + row.Premium + ":" + row.Count));
            profile.CommitFailure = phase => { if (phase == stage) throw new IOException("premium pack injected " + stage); };
            Reject(() => profile.BeginNativePacks(FairPackRules.Standard, mass ? 40 : 1), "atomic " + mass + "/" + stage);
            profile.CommitFailure = null;
            Check(bytes.SequenceEqual(File.ReadAllBytes(path)) && !profile.PackRevealPending && profile.PackSequence == 9
                && profile.UnopenedPacks == 40 && owned == string.Join(",", profile.OwnedCards.Select(row => row.Id + ":" + row.Premium + ":" + row.Count))
                && profile.PackCounters.Single(row => row.PackType == FairPackRules.Standard).Counters.PacksOpened == 9, "failure changed file or memory");
        }
        string batchPath = Copy("batch"), singlesPath = Copy("singles"); string[] batchKeys;
        using (var profile = new LocalProfile(batchPath)) {
            var batch = profile.BeginNativePacks(FairPackRules.Standard, 40);
            batchKeys = NativeUIProjection.PendingReceipt(batch);
            Check(batch.SelectMany(pack => pack.Cards).Any(card => card.Premium == 1) && batch.SelectMany(pack => pack.Cards).Any(card => card.Premium == 3), "batch must contain both qualities");
        }
        using (var profile = new LocalProfile(singlesPath)) {
            var keys = new List<string>();
            for (int i = 0; i < 40; i++) {
                var pack = profile.BeginNativePack(FairPackRules.Standard); keys.AddRange(pack.Cards.Select(card => card.Key));
                profile.CompleteNativePackReveal(profile.PackSequence, pack.Cards.Select(card => card.Key));
            }
            Check(keys.SequenceEqual(batchKeys), "quality differs between batch and single opens");
        }
        byte[] pending = File.ReadAllBytes(batchPath);
        using (var profile = new LocalProfile(batchPath)) {
            Check(pending.SequenceEqual(File.ReadAllBytes(batchPath)) && profile.PendingPacks.Count == 40
                && NativeUIProjection.PendingReceipt(profile.BeginNativePacks(FairPackRules.Standard, 1)).SequenceEqual(batchKeys), "batch rerolled or rewrote on restart");
            profile.CompleteNativePackReveal(profile.PackSequence, batchKeys);
        }
        Check(File.ReadAllBytes(batchPath).SequenceEqual(File.ReadAllBytes(singlesPath)), "premium batch and singles settled bytes differ");
        DeferredPortraits();
        foreach (int count in new[] { 1, 40 }) DeferredProgression(Copy("deferred-" + count), count);
        AllCosmetics(Copy("cosmetics")); MasteryPackCopy(Copy("mastery-pack")); SignatureMeta(Copy("signature-meta"));
        Protection(Copy("protection")); Extras(Copy("extras")); RuleOne(Copy("v1"));
        Console.WriteLine("PASS premium packs: Golden/Signature single and 40-pack replay, exact bytes vs singles, write/flush/replace atomicity, quality provenance, versioned native deltas, exact-version deck ownership/repair, half-cost disenchant, cross-quality lifetime protection, rule-1 replay/XML, deferred settlement/load progression, portraits, cheat floors, meta switch, sellable copies do not reduce mastery");
    }
    private static void DeferredPortraits()
    {
        var grant = typeof(LocalProfile).GetMethod("GrantClassRewards", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        foreach (int count in new[] { LocalClassRewards.GoldenPortraitWins, LocalClassRewards.HonoredPortraitWins }) {
            var data = new LocalProfileData { PackRevealPending = true,
                ClassXp = LocalCoreProgression.Heroes.Select(hero => new LocalClassXp { Hero = hero }).ToList(),
                Matches = Enumerable.Range(0, count).Select(_ => new LocalMatchRecord { Result = "Won", DeckHero = "HERO_08", ClassXpBefore = 0, ClassXpAfter = 1 }).ToList() };
            Check(!(bool)grant.Invoke(null, new object[] { data, null }) && data.PortraitGrants.Count == 0 && data.PremiumCollection.Count == 0,
                "pending reveal claims portrait");
            data.PackRevealPending = false;
            Check((bool)grant.Invoke(null, new object[] { data, null }) && data.PortraitGrants.Count == (count == LocalClassRewards.GoldenPortraitWins ? 1 : 2)
                && data.PremiumCollection.Single(row => row.Id == "HERO_08" && row.Premium == 1).Count == 1, "deferred portrait not granted");
            Check(!(bool)grant.Invoke(null, new object[] { data, null }), "deferred portrait granted twice");
        }
    }
    private static LocalMatchRecord Win(LocalProfile profile, LocalCustomDeck deck, int seed)
    {
        var played = new LocalDeck(deck.Name, deck.Hero, deck.Cards.ToArray());
        var context = profile.AchievementContextFor(played, true, "npc-mastery", 2500, "Diamond");
        var pending = profile.BeginLaunch("custom:" + deck.Id, "priest", seed, "npc-mastery", "priest", 2500, context, _ => { });
        var frozen = pending.AchievementContext;
        var evidence = new LocalAchievementMatchEvidence(pending.Id, "Won", frozen.StartingHero, frozen.QueuedNpcId, frozen.QueuedNpcBand,
            false, false, false, LocalAchievementContextBinding.Create(frozen), new string('A', 64), true, 1, 2, 3, 4
#if ENGINE_SOURCE
            , null
#endif
            ).Invalidated("No engine facts in premium pack fixture.");
        var match = profile.Settle(pending.Id, "Won", 4, false, null, null, null, evidence);
        profile.AcknowledgeRewardPresentation(match.Id);
        return match;
    }
    private static void DeferredProgression(string path, int count)
    {
        const string target = "TIME_860";
        var targetReward = LocalClassRewards.ActiveRewards.Single(row => row.CardId == target && row.Hero == "HERO_08");
        int threshold = LocalCardMastery.Tiers(target, 0).First().RequiredWins;
        var rarity = Cards.FromId(target).Rarity;
        var mateReward = LocalClassRewards.ActiveRewards.First(row => row.Hero == "HERO_08" && row.Premium == 1 && row.CardId != target
            && FairPackRules.Eligible(FairPackRules.Standard, 2).Any(card => card.Id == row.CardId && card.Rarity == rarity));
        string mate = mateReward.CardId;
        var core = LocalShop.StandardCollectionCards.Where(card => LocalCardCatalog.IsCore(card.Id) && card.Rarity != Rarity.LEGENDARY
            && LocalDecks.IsAllowedInDeck(card, CardClass.MAGE)).Take(14).Select(card => card.Id).ToArray();
        string[] keys; LocalCustomDeck deck; string beforeLedger, beforePremium, beforeNormal;
        using (var profile = new LocalProfile(path)) {
            profile.CommitChange(next => { foreach (string id in core) Add(next, id, 0, 2); Add(next, target, 0, 1); Add(next, mate, 0, 1); return true; });
            deck = profile.CreateDraft("Pending progression", "HERO_08");
            profile.SaveDraft(deck.Id, deck.Name, deck.Hero, core.SelectMany(id => new[] { id, id }).Concat(new[] { target, mate }), 0);
            deck = profile.GetDraft(deck.Id);
            for (int i = 0; i < threshold - 1; i++) Win(profile, deck, i);
            beforeLedger = Ledger(Read(path));
            var packs = profile.BeginNativePacks(FairPackRules.Standard, count); keys = NativeUIProjection.PendingReceipt(packs);
            Check(packs[0].Cards.Any(card => card.Id == target && card.Premium == 1)
                && packs[0].Cards.All(card => card.Id != mate), "deferred fixture needs pack Golden and rarity bucket-mate");
            beforePremium = Premium(Read(path)); beforeNormal = Normal(Read(path));
            profile.CommitChange(next => { next.ClassXp.Single(row => row.Hero == "HERO_08").Xp =
                LocalCoreProgression.LevelThresholds[Math.Max(targetReward.Level, mateReward.Level) - 1] - LocalCoreProgression.Xp("Won", false); return true; });
        }
        using (var profile = new LocalProfile(path)) {
            Check(Ledger(Read(path)) == beforeLedger && Premium(Read(path)) == beforePremium, "pending load grants class rewards");
            var settled = Win(profile, profile.GetDraft(deck.Id), threshold);
            Check((settled.MasteryGrants?.Count ?? 0) == 0 && (settled.ClassRewardGrants?.Count ?? 0) == 0
                && Ledger(Read(path)) == beforeLedger && Premium(Read(path)) == beforePremium, "pending settlement grants progression");
        }
        using (var profile = new LocalProfile(path)) {
            Check(keys.SequenceEqual(NativeUIProjection.PendingReceipt(profile.PendingPacks)) && Ledger(Read(path)) == beforeLedger
                && Premium(Read(path)) == beforePremium && Normal(Read(path)) == beforeNormal, "pending settlement/load changes replay or grants");
            profile.CompleteNativePackReveal(profile.PackSequence, keys);
            var saved = Read(path);
            Check(saved.CoreGrants.All(grant => !FairPackRules.Eligible(FairPackRules.Standard, 2).Any(card => card.Id == grant.Id)),
                "Core catch-up touches pack-eligible counts");
            foreach (string id in new[] { target, mate }) {
                Check(saved.ClassRewardGrants.Count(grant => grant.CardId == id) == 1
                    && saved.MasteryGrants.Count(grant => grant.CardId == id && grant.Premium == 1 && grant.MatchId == null) == 1
                    && profile.ProgressionGrantedCount(id, 1) == 2, "ack did not catch up exactly once " + id);
            }
            Check((saved.Matches.Last().MasteryGrants?.Count ?? 0) == 0, "catch-up rewrites settled mastery receipt");
        }
        byte[] stable = File.ReadAllBytes(path);
        using (var profile = new LocalProfile(path)) { profile.CommitChange(next => true); Check(!profile.CompleteNativePackReveal(profile.PackSequence, profile.LastPack.Cards.Select(card => card.Key)), "ack replay paid twice"); }
        Check(stable.SequenceEqual(File.ReadAllBytes(path)), "catch-up repeated after ack/reload");
    }
    private static string Normal(LocalProfileData data) => string.Join(",", data.Collection.Select(row => row.Id + ":" + row.Count + ":" + row.Acquired));
    private static string Premium(LocalProfileData data) => string.Join(",", data.PremiumCollection.Select(row => row.Id + ":" + row.Premium + ":" + row.Count + ":" + row.Acquired));
    private static string Ledger(LocalProfileData data) => string.Join(",", data.ClassRewardGrants.Select(row => row.Hero + ":" + row.Level + ":" + row.Copies))
        + "|" + string.Join(",", data.MasteryGrants.Select(row => row.CardId + ":" + row.Premium + ":" + row.Copies));
    private static void AllCosmetics(string path)
    {
        using var profile = new LocalProfile(path, new ProfileOptions { AllCosmetics = true });
        var native = profile.OwnedCards.ToDictionary(row => LocalPackCard.ReceiptKey(row.Id, row.Premium), row => row.Count);
        int NativeCount(string id, int premium) => native.TryGetValue(LocalPackCard.ReceiptKey(id, premium), out int value) ? value : 0;
        var before = profile.StoredOwnedCards.ToDictionary(row => LocalPackCard.ReceiptKey(row.Id, row.Premium), row => row.Count);
        var pack = profile.BeginNativePack(FairPackRules.Standard);
        var delta = NativeUIProjection.NewGrantCounts(pack.Cards);
        Check(pack.Cards.Any(card => card.Premium == 1 && profile.OwnedCount(card.Id, 1) > profile.StoredOwnedCount(card.Id, 1)), "cheat fixture lacks floored Golden");
        foreach (var pair in delta) {
            var card = LocalPackCard.FromKey(pair.Key);
            Check(profile.StoredOwnedCount(card.Id, card.Premium) - (before.TryGetValue(pair.Key, out int value) ? value : 0) == pair.Value, "cheat floor hides durable pack delta");
        }
        foreach (var card in NativeUIProjection.PackGrantDeltas(profile, delta, NativeCount))
            native[LocalPackCard.ReceiptKey(card.Id, card.Premium)] = NativeCount(card.Id, card.Premium) + card.Count;
        Check(delta.Keys.All(key => { var card = LocalPackCard.FromKey(key); return NativeCount(card.Id, card.Premium) == profile.OwnedCount(card.Id, card.Premium); }), "native cheat floor inflated by pack grants");
        profile.CompleteNativePackReveal(profile.PackSequence, pack.Cards.Select(card => card.Key));
        string core = LocalCoreProgression.ById.Keys.First(id => profile.OwnedCount(id) == 0);
        profile.CommitChange(next => { next.CoreGrants.Add(new LocalCardCount { Id = core, Count = 1 }); return true; });
        Check(profile.StoredOwnedCount(core, 0) == 0 && NativeUIProjection.PackGrantDeltas(profile,
            new Dictionary<string, int> { [core] = 1 }, NativeCount).Single().Count == 1, "shared native grant path lost shop Core grants");
    }
    private static void MasteryPackCopy(string path)
    {
        const string id = "TIME_860";
        using var profile = new LocalProfile(path);
        var pack = profile.BeginNativePack(FairPackRules.Standard);
        Check(pack.Cards.Any(card => card.Id == id && card.Premium == 1), "mastery fixture lacks pack Golden");
        profile.CompleteNativePackReveal(profile.PackSequence, pack.Cards.Select(card => card.Key));
        profile.CommitChange(next => {
            var mastery = new LocalProfileData { PremiumCollection = next.PremiumCollection };
            mastery.Matches = Enumerable.Range(0, LocalCardMastery.Tiers(id, 0).First().RequiredWins).Select(_ => new LocalMatchRecord { Result = "Won", Turns = 4,
                AchievementContext = new LocalAchievementPendingContext { PersonalDeck = true, EligibleWorldMatch = true,
                    MainDeck = new() { new LocalAchievementDeckCard { BaseCardId = id } } } }).ToList();
            LocalCardMastery.Grant(mastery, null);
            next.MasteryGrants.AddRange(mastery.MasteryGrants);
            return true;
        });
        Check(profile.ProgressionGrantedCount(id, 1) == 2 && profile.StoredOwnedCount(id, 1) == 3
            && profile.DisenchantablePremiumCount(id, 1) == 1, "pack copy reduced mastery playset");
        Check(LocalCrafting.CommitPremiumBatch(profile, id, 1, CraftDirection.Disenchant, 1, 3).Ok
            && profile.StoredOwnedCount(id, 1) == 2 && profile.DisenchantablePremiumCount(id, 1) == 0, "selling pack copy leaves incomplete mastery playset");
    }
    private static void SignatureMeta(string path)
    {
        string previous = LocalClassRewards.ActiveMetaOverride; string[] keys;
        try {
            LocalClassRewards.ActiveMetaOverride = "violet-hold";
            using (var profile = new LocalProfile(path)) {
                keys = NativeUIProjection.PendingReceipt(new[] { profile.BeginNativePack(FairPackRules.Standard) });
                Check(keys.Any(key => key.EndsWith(":3")), "meta fixture lacks Signature");
            }
            foreach (string meta in new[] { "black-empire", "violet-hold" }) {
                LocalClassRewards.ActiveMetaOverride = meta;
                using var profile = new LocalProfile(path);
                Check(keys.SequenceEqual(NativeUIProjection.PendingReceipt(profile.PendingPacks)), "meta switch changed pending Signature");
            }
            using (var profile = new LocalProfile(path)) profile.CompleteNativePackReveal(profile.PackSequence, keys);
            foreach (string meta in new[] { "black-empire", "violet-hold" }) {
                LocalClassRewards.ActiveMetaOverride = meta;
                using var profile = new LocalProfile(path);
                Check(keys.SequenceEqual(profile.LastPack.Cards.Select(card => card.Key)), "meta switch rejected settled Signature");
            }
        } finally { LocalClassRewards.ActiveMetaOverride = previous; }
    }
    private static void Single(string path)
    {
        string[] keys; LocalPackCard golden, signature; long deckId; string hero;
        using (var profile = new LocalProfile(path)) {
            var expected = FairPackRules.Generate(FairPackRules.Standard, 2, (ulong)Read(path).PackSeed, 9, Before, _ => 0);
            string target = expected.CardIds[expected.Premiums.IndexOf(1)];
            var cardClass = Cards.FromId(target).Class;
            hero = LocalCoreProgression.Heroes.First(id => cardClass == CardClass.NEUTRAL || Cards.FromId(id).Class == cardClass);
            var draft = profile.CreateDraft("Pack keeps version", hero); deckId = draft.Id;
            profile.SaveDraft(deckId, draft.Name, hero, new[] { target }, 0);
            var pack = profile.BeginNativePack(FairPackRules.Standard);
            golden = pack.Cards.First(card => card.Premium == 1); signature = pack.Cards.First(card => card.Premium == 3);
            keys = NativeUIProjection.PendingReceipt(profile.PendingPacks);
            Check(pack.PackRuleVersion == 2 && pack.Cards.All(card => card.DuplicateGold == 0), "wrong rule/duplicate gold");
            Check(profile.GetDraft(deckId).CardEntries.Single().Premium == 1 && profile.MissingDeckSlotCount(deckId) == 0
                && !NativeUIProjection.ShouldRefreshPremiumDecks(profile, golden.Id, 1), "pack repairs missing Normal without a progression upgrade");
            var saved = Read(path);
            Check(pack.Cards.GroupBy(card => card.Key).All(group => {
                var card = group.First(); var rows = card.Premium == 0 ? saved.Collection : saved.PremiumCollection;
                var row = rows.Single(entry => entry.Id == card.Id && entry.Premium == card.Premium);
                return row.Count == group.Count() && row.Acquired == Math.Min(card.Premium == 0 ? LocalCrafting.Playset(card.Id) : 2, group.Count());
            }), "pack credited wrong collection/acquisition");
            Check(pack.Cards.All(card => profile.SeenCopies(card.Id, card.Premium) == 0), "Normal/premium pack copies must start NEW");
            Check(profile.ProgressionGrantedCount(golden.Id, 1) == 0 && profile.ProgressionGrantedCount(signature.Id, 3) == 0, "pack counted as progression");
            var delta = NativeUIProjection.NewGrantCounts(pack.Cards);
            Check(delta[golden.Key] > 0 && delta[signature.Key] > 0 && !delta.ContainsKey(golden.Id), "native delta loses quality");
            Check(NativeUIProjection.ReceiptMatchesActors(keys, keys.Reverse().ToArray())
                && !NativeUIProjection.ReceiptMatchesActors(keys, pack.Cards.Select(card => card.Id).ToArray()), "native actors ignore quality");
            Reject(() => profile.CompleteNativePackReveal(profile.PackSequence, pack.Cards.Select(card => card.Id)), "quality-less ack");
            Check(!LocalCrafting.CommitPremiumBatch(profile, golden.Id, 1, CraftDirection.Disenchant, 1, profile.OwnedCount(golden.Id, 1)).Ok, "pending sale allowed");
        }
        byte[] bytes = File.ReadAllBytes(path);
        var tampered = Read(path);
        var changed = tampered.LastPack.Cards.First(card => card.Premium == 3);
        var old = tampered.PremiumCollection.Single(row => row.Id == changed.Id && row.Premium == 3);
        old.Count--; old.Acquired = changed.AcquiredBefore;
        changed.Premium = 1; Add(tampered, changed.Id, 1, 1);
        string bad = path + ".tampered"; Write(bad, tampered);
        byte[] badBytes = File.ReadAllBytes(bad);
        Reject(() => { using var ignored = new LocalProfile(bad); }, "coherent quality tamper");
        Check(badBytes.SequenceEqual(File.ReadAllBytes(bad)), "tamper refusal rewrites save");
        using (var profile = new LocalProfile(path)) {
            Check(profile.PackRevealPending && keys.SequenceEqual(NativeUIProjection.PendingReceipt(profile.PendingPacks))
                && bytes.SequenceEqual(File.ReadAllBytes(path)), "single rerolled on restart");
            profile.CompleteNativePackReveal(profile.PackSequence, keys);
            profile.SavePremiumDraft(deckId, "Pack owned", hero, new[] { new LocalDeckCard { Id = golden.Id, Premium = 1 } }, 0);
            Check(profile.SavedVersionsOwned(deckId) && profile.TotalOwnedCount(golden.Id) >= 1, "pack premium not owned for decks");
            foreach (var card in new[] { golden, signature }) {
                int count = profile.OwnedCount(card.Id, card.Premium); long dust = profile.DustBalance;
                int craft = Cards.FromId(card.Id).Rarity == Rarity.COMMON ? 400 : Cards.FromId(card.Id).Rarity == Rarity.RARE ? 800
                    : Cards.FromId(card.Id).Rarity == Rarity.EPIC ? 1600 : 3200;
                Check(LocalCrafting.TryGetPrice(card.Id, card.Premium, out _, out int sale) && sale == craft / 2, "half craft cost");
                Check(profile.DisenchantablePremiumCount(card.Id, card.Premium) == count
                    && LocalCrafting.CommitPremiumBatch(profile, card.Id, card.Premium, CraftDirection.Disenchant, 1, count).Ok
                    && profile.OwnedCount(card.Id, card.Premium) == count - 1 && profile.DustBalance == dust + sale, "premium pack disenchant");
                Check(Read(path).PremiumCollection.Single(row => row.Id == card.Id && row.Premium == card.Premium).Acquired >= 1, "sale lost lifetime acquisition");
            }
        }
        bytes = File.ReadAllBytes(path);
        using (var profile = new LocalProfile(path)) Check(!profile.PackRevealPending && bytes.SequenceEqual(File.ReadAllBytes(path)), "ack/sale restart changed receipt");
    }
    private static void Protection(string path)
    {
        using var profile = new LocalProfile(path);
        var data = Read(path);
        var unprotected = FairPackRules.Generate(FairPackRules.Standard, 2, (ulong)data.PackSeed, 9, Before, _ => 0);
        var protectedIds = unprotected.CardIds.Distinct().ToArray();
        profile.CommitChange(next => {
            foreach (string id in protectedIds) {
                int cap = LocalCrafting.Playset(id);
                if (cap == 2) Add(next, id, 0, 1);
                Add(next, id, 1, 1);
            }
            return true;
        });
        var pack = profile.BeginNativePack(FairPackRules.Standard);
        Check(pack.Cards.All(card => !protectedIds.Contains(card.Id)), "normal plus premium full playset did not protect");
        profile.CompleteNativePackReveal(profile.PackSequence, pack.Cards.Select(card => card.Key));
        foreach (string id in protectedIds) Check(LocalCrafting.CommitPremiumBatch(profile, id, 1, CraftDirection.Disenchant, 1, 1).Ok, "protection fixture sale");
        var afterSale = profile.BeginNativePack(FairPackRules.Standard);
        Check(afterSale.Cards.All(card => !protectedIds.Contains(card.Id)), "disenchanted premium lost protection");
    }
    private static void Extras(string path)
    {
        LocalPackCard golden, signature;
        using (var profile = new LocalProfile(path)) {
            var draw = FairPackRules.Generate(FairPackRules.Standard, 2, (ulong)Read(path).PackSeed, 9, Before, _ => 0);
            golden = new LocalPackCard { Id = draw.CardIds[draw.Premiums.IndexOf(1)], Premium = 1 };
            signature = new LocalPackCard { Id = draw.CardIds[draw.Premiums.IndexOf(3)], Premium = 3 };
            profile.CommitChange(next => {
                foreach (var card in FairPackRules.Eligible(FairPackRules.Standard, 2)) {
                    var row = next.Collection.SingleOrDefault(entry => entry.Id == card.Id);
                    if (row == null) { row = new LocalCardCount { Id = card.Id }; next.Collection.Add(row); }
                    row.Acquired = LocalCrafting.Playset(card.Id);
                }
                Add(next, golden.Id, 1, 2); Add(next, signature.Id, 3, 1); return true;
            });
            int gold = profile.StoredGoldBalance;
            var pack = profile.BeginNativePack(FairPackRules.Standard);
            Check(pack.Cards.All(card => card.DuplicateGold == 0) && profile.StoredGoldBalance == gold
                && profile.OwnedCount(golden.Id, 1) == 3 && profile.OwnedCount(signature.Id, 3) == 2,
                "premium extras converted to gold or failed to credit");
        }
        using (var profile = new LocalProfile(path)) {
            Check(profile.OwnedCount(golden.Id, 1) == 3 && profile.OwnedCount(signature.Id, 3) == 2, "premium extras did not replay");
            profile.CompleteNativePackReveal(profile.PackSequence, profile.LastPack.Cards.Select(card => card.Key));
        }
        var tampered = Read(path);
        var missing = FairPackRules.Eligible(FairPackRules.Standard, 2).First(card => card.Id != golden.Id && card.Rarity == Cards.FromId(golden.Id).Rarity && card.Set == Cards.FromId(golden.Id).Set);
        tampered.Collection.Single(card => card.Id == missing.Id).Acquired = 0;
        string bad = path + ".incomplete"; Write(bad, tampered);
        Reject(() => { using var ignored = new LocalProfile(bad); }, "premium extras without complete rarity acquisition");
        using var sale = new LocalProfile(path);
        Check(sale.DisenchantablePremiumCount(golden.Id, 1) == 3
            && LocalCrafting.CommitPremiumBatch(sale, golden.Id, 1, CraftDirection.Disenchant, 1, 3).Ok, "premium extra is not disenchantable");
    }
    private static void RuleOne(string path)
    {
        using (var profile = new LocalProfile(path)) profile.CommitChange(next => {
            var draw = FairPackRules.Generate(FairPackRules.Standard, 1, (ulong)next.PackSeed, 9, Before,
                id => next.Collection.SingleOrDefault(card => card.Id == id)?.Acquired ?? 0);
            Add(next, draw.CardIds[0], 1, 1);
            var pack = new LocalPackResult { PackType = FairPackRules.Standard, PackRuleVersion = 1, CountersBefore = Before.Copy(),
                Cards = draw.CardIds.Select(id => new LocalPackCard { Id = id, AcquiredBefore = next.Collection.SingleOrDefault(card => card.Id == id)?.Acquired ?? 0 }).ToList() };
            foreach (string id in draw.CardIds) Add(next, id, 0, 1);
            next.LastPack = pack; next.PackRevealPending = true; next.PackSequence++;
            next.PackCounters.Single(row => row.PackType == FairPackRules.Standard).Counters = draw.After;
            next.UnopenedPacks.Single(row => row.PackType == FairPackRules.Standard).Count--;
            return true;
        });
        byte[] bytes = File.ReadAllBytes(path);
        using var loaded = new LocalProfile(path);
        Check(loaded.LastPack.PackRuleVersion == 1 && loaded.LastPack.Cards.All(card => card.Premium == 0)
            && bytes.SequenceEqual(File.ReadAllBytes(path)), "rule 1 pending replay changed with premium owned");
        using var xml = new StringWriter(); new XmlSerializer(typeof(LocalPackResult)).Serialize(xml, loaded.LastPack);
        Check(!xml.ToString().Contains("<Premium>") && !xml.ToString().Contains("<Key>"), "rule 1 XML changed");
        loaded.CompleteNativePackReveal(loaded.PackSequence, loaded.LastPack.Cards.Select(card => card.Key));
    }
}
