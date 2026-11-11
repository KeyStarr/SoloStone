using System.Xml.Serialization;
using SabberStoneCore.Model;

internal static class DeckPremiumChecks
{
    private static readonly XmlSerializer Serializer = new(typeof(LocalProfileData));
    private static readonly Dictionary<string, int> BurnGolden = new() {
        ["FIR_910"] = 1, ["FIR_911"] = 2, ["TLC_364"] = 2, ["CATA_489"] = 2
    };
    private static void Check(bool ok, string message) { if (!ok) throw new Exception("Deck premiums: " + message); }
    private static string TestPath(string root, string name)
    {
        string directory = Path.Combine(root, name); Directory.CreateDirectory(directory);
        return Path.Combine(directory, "profile.xml");
    }
    private static LocalProfileData Read(string path) { using var stream = File.OpenRead(path); return (LocalProfileData)Serializer.Deserialize(stream); }
    private static void Write(string path, LocalProfileData data) { using var stream = File.Create(path); Serializer.Serialize(stream, data); }
    private static LocalDeckCard Slot(string id, int premium = 0) => new() { Id = id, Premium = premium };
    private static void Own(LocalProfileData data, string id, int premium, int count)
    {
        var rows = premium == 0 ? data.Collection : data.PremiumCollection;
        var row = rows.SingleOrDefault(card => card.Id == id && card.Premium == premium);
        if (row == null) { row = new LocalCardCount { Id = id, Premium = premium }; rows.Add(row); }
        row.Count = count; row.Acquired = Math.Max(row.Acquired, count);
    }
    private static LocalCustomDeck Recipe(string id)
    {
        var built = LocalShop.Default.Library.BuildDeck(LocalShop.Default.Recipe(id), _ => 0).Deck;
        return new LocalCustomDeck { Name = built.Name, Hero = built.Hero, Cards = built.CardIds,
            SourceRecipeId = id, Sideboards = built.Sideboards.Select(board =>
                new LocalSideboard { Source = board.Key, Cards = board.Value.ToList() }).ToList() };
    }
    internal static void Run(string root)
    {
        using var cosmetics = new CosmeticsFixture();
        Allocation(); PreferBest(); SandboxBestVersions(root); InPlaceKeepsChoices(root); ZeroCopyMastery(); SandboxMastery(root); LoadRepair(root); Disenchant(root);
        foreach (string recipe in new[] { "burn-mage", "rafaam-warlock", "companion-hunter", "maiev-aggro-paladin", "captured-archmage-paladin", "quest-murloc-paladin" })
            Purchase(root, recipe);
        DuplicateChoices(root);
        Console.WriteLine("PASS deck premiums: exact copies, sideboards, Fabled, zero-copy mastery, repair/reload, crafting, purchases/reuse/replay");
    }

    private static void Allocation()
    {
        const string id = "FIR_911";
        var deck = new LocalCustomDeck { CardEntries = new() { Slot(id), Slot(id) } };
        var data = new LocalProfileData(); Own(data, id, 0, 1); Own(data, id, 1, 2);
        LocalCrafting.AssignOwnedPremiums(deck, LocalProfile.DeckOwnedCount(data));
        Check(deck.CardEntries.Select(card => card.Premium).SequenceEqual(new[] { 0, 1 }), "one normal plus one golden");
        Check(LocalCrafting.AssignOwnedPremiums(deck, LocalProfile.DeckOwnedCount(data)).Count == 0, "idempotent assignment");
        deck = new LocalCustomDeck { CardEntries = new() { Slot(id), Slot(id, 1) },
            Sideboards = new() { new LocalSideboard { Source = "JAIL_397", CardEntries = new() { Slot(id) } } } };
        Own(data, id, 1, 1);
        LocalCrafting.AssignOwnedPremiums(deck, LocalProfile.DeckOwnedCount(data));
        Check(deck.CardEntries[1].Premium == 1 && LocalCrafting.MissingDeckSlots(deck, LocalProfile.DeckOwnedCount(data)) == 1,
            "reserve explicit premium and do not reuse main copies in sideboard");
        Own(data, id, 1, 2);
        LocalCrafting.AssignOwnedPremiums(deck, LocalProfile.DeckOwnedCount(data));
        Check(deck.Sideboards[0].CardEntries[0].Premium == 1
            && LocalCrafting.MissingDeckSlots(deck, LocalProfile.DeckOwnedCount(data)) == 0, "sideboard free golden");
        deck.Sideboards[0].Source = "JAIL_831";
        Own(data, id, 1, 1);
        Check(LocalCrafting.MissingDeckSlots(deck, LocalProfile.DeckOwnedCount(data)) == 1, "Underbelly shares exact-version budget too");
        var best = new LocalCustomDeck { CardEntries = new() { Slot(id) } };
        LocalCrafting.AssignOwnedPremiums(best, (card, premium) => premium == 2 || premium == 3 ? 1 : 0);
        Check(best.CardEntries[0].Premium == 2, "Diamond before Signature");
#if ENGINE_SOURCE
        var primary = Cards.All.First(card => LocalDecks.FabledBundle(card.Id).Count > 0);
        var bundle = LocalDecks.FabledBundle(primary.Id);
        var fabled = new LocalCustomDeck { CardEntries = new[] { primary.Id }.Concat(bundle).Select(card => Slot(card)).ToList() };
        Func<string, int, int> owned = (card, premium) => card == primary.Id && premium == 1 ? 1 : 0;
        LocalCrafting.AssignOwnedPremiums(fabled, owned);
        Check(fabled.CardEntries.All(card => card.Premium == 1) && LocalCrafting.MissingDeckSlots(fabled, owned) == 0,
            "Fabled companions inherit primary premium without collection grants");
        LocalCrafting.AssignOwnedPremiums(fabled, (card, premium) => card == primary.Id && premium == 0 ? 1 : 0);
        Check(fabled.CardEntries.All(card => card.Premium == 0), "Fabled reverse premium repair");
        var fabledBest = new LocalCustomDeck { CardEntries = new[] { primary.Id }.Concat(bundle).Select(card => Slot(card)).ToList() };
        Func<string, int, int> ownedBoth = (card, premium) => card == primary.Id && premium <= 1 ? 1 : 0;
        LocalCrafting.AssignOwnedPremiums(fabledBest, ownedBoth, true);
        Check(fabledBest.CardEntries.All(card => card.Premium == 1) && LocalCrafting.MissingDeckSlots(fabledBest, ownedBoth) == 0,
            "preferBest: Fabled companions follow the primary's best version");
#endif
    }

    // Fresh decks (preferBest) take the fanciest owned versions first, sharing one budget across main deck and sideboards.
    private static void PreferBest()
    {
        const string id = "FIR_911";
        var data = new LocalProfileData(); Own(data, id, 0, 4); Own(data, id, 1, 2); Own(data, id, 2, 1); Own(data, id, 3, 1);
        var deck = new LocalCustomDeck { CardEntries = new() { Slot(id), Slot(id), Slot(id) },
            Sideboards = new() { new LocalSideboard { Source = "JAIL_397", CardEntries = new() { Slot(id) } } } };
        var owned = LocalProfile.DeckOwnedCount(data);
        Check(LocalCrafting.AssignOwnedPremiums(deck, owned, true).Count == 4, "preferBest reports the four upgraded slots");
        Check(deck.CardEntries.Select(card => card.Premium).SequenceEqual(new[] { 2, 3, 1 })
            && deck.Sideboards[0].CardEntries[0].Premium == 1, "preferBest: best quality first (premium 2, then 3, then Golden), shared sideboard budget");
        Check(LocalCrafting.AssignOwnedPremiums(deck, owned, true).Count == 0, "preferBest idempotent");
        var plain = new LocalCustomDeck { CardEntries = new() { Slot(id), Slot(id) } };
        LocalCrafting.AssignOwnedPremiums(plain, owned);
        Check(plain.CardEntries.All(card => card.Premium == 0), "default mode keeps owned explicit Normal");
        var none = new LocalCustomDeck { CardEntries = new() { Slot(id) } };
        Check(LocalCrafting.AssignOwnedPremiums(none, (card, premium) => 0, true).Count == 0 && none.CardEntries[0].Premium == 0,
            "preferBest keeps unowned slots");
    }

    // Sandbox All cards + All decks (+ All cosmetics): every granted deck takes the best supported version.
    private static void SandboxBestVersions(string root)
    {
        foreach (bool cosmetics in new[] { true, false }) {
            string path = TestPath(root, "deck-premium-sandbox-best-" + cosmetics);
            var options = new ProfileOptions { AllCards = true, AllDecks = true, AllCosmetics = cosmetics };
            using (var profile = new LocalProfile(path, options)) profile.PersistNewProfile(options);
            var data = Read(path);
            Check(data.CustomDecks.Count > 0, "All decks granted decks");
            int fancy = 0, normal = 0;
            var owned = LocalProfile.DeckOwnedCount(data, cosmetics);
            Func<LocalProfileData, string> versions = d => string.Join("|", d.CustomDecks.Select(deck => deck.Id + ":" + string.Join(",",
                deck.CardEntries.Concat(deck.Sideboards.SelectMany(board => board.CardEntries)).Select(card => card.Id + "=" + card.Premium))));
            foreach (var deck in data.CustomDecks) {
                var entries = deck.CardEntries.Concat(deck.Sideboards.SelectMany(board => board.CardEntries)).ToList();
                var companions = new HashSet<string>(entries.SelectMany(card => LocalDecks.FabledBundle(card.Id)), StringComparer.Ordinal);
                Check(LocalCrafting.MissingDeckSlots(deck, owned) == 0, "deck " + deck.Name + " has no missing slots (cosmetics=" + cosmetics + ")");
                foreach (string companion in companions.Where(id => entries.Any(card => card.Id == id))) {
                    var followed = entries.Where(card => LocalDecks.FabledBundle(card.Id).Contains(companion)).Select(card => card.Premium).OrderBy(v => v);
                    Check(entries.Where(card => card.Id == companion).Select(card => card.Premium).OrderBy(v => v).SequenceEqual(followed),
                        "deck " + deck.Name + " companion " + companion + " follows its primary (cosmetics=" + cosmetics + ")");
                }
                foreach (var group in entries.Where(card => !companions.Contains(card.Id)).GroupBy(card => card.Id)) {
                    Func<int, int> have = version => version == 0 || !LocalClassRewards.IsAvailable(group.Key) ? 0 : Math.Max(
                        data.PremiumCollection.Where(row => row.Id == group.Key && row.Premium == version).Sum(row => row.Count),
                        cosmetics ? LocalProfile.CheatPremiumCount(group.Key, version) : 0);
                    var slots = group.OrderByDescending(card => LocalCrafting.PremiumQuality(card.Premium)).ToList();
                    var expected = new List<int>();
                    foreach (int version in Enumerable.Range(1, 3).OrderByDescending(LocalCrafting.PremiumQuality))
                        for (int copy = 0; copy < have(version) && expected.Count < slots.Count; copy++) expected.Add(version);
                    while (expected.Count < slots.Count) expected.Add(0);
                    Check(slots.Select(card => card.Premium).SequenceEqual(expected),
                        "deck " + deck.Name + " " + group.Key + " best versions (cosmetics=" + cosmetics + ")");
                    fancy += slots.Count(card => card.Premium != 0); normal += slots.Count(card => card.Premium == 0);
                }
            }
            Check(data.CustomDecks.Any(deck => deck.Sideboards.Count > 0), "granted decks include sideboards");
            if (cosmetics) Check(fancy > normal, "All cosmetics decks are mostly premium, not Normal");
            else Check(!data.CustomDecks.SelectMany(deck => deck.CardEntries.Concat(deck.Sideboards.SelectMany(board => board.CardEntries))).Any(card => card.Premium >= 2),
                "no premium 2/3 without cosmetics");
            // Load repair leaves the best versions alone.
            string before = versions(data);
            using (var reopened = new LocalProfile(path, options)) { }
            Check(versions(Read(path)) == before, "reload keeps the granted versions (cosmetics=" + cosmetics + ")");
        }
    }

    // A recipe deck the player already built keeps its explicit Normal choices when the shop deck completes it in place.
    private static void InPlaceKeepsChoices(string root)
    {
        string path = TestPath(root, "deck-premium-inplace-keeps.xml");
        using var profile = new LocalProfile(path);
        profile.GrantWelcomeIfNew();
        profile.CommitChange(data => {
            data.GoldBalance = 100000;
            foreach (var row in BurnGolden) { Own(data, row.Key, 0, 2); Own(data, row.Key, 1, 2); }
            return true;
        });
        profile.Purchase("recipe-burn-mage", 1, profile.PurchasePrice("recipe-burn-mage"), "recipe");
        var deck = profile.SaveDeckFromRecipe("burn-mage", Recipe("burn-mage"));
        Check(deck.CardEntries.Where(card => BurnGolden.ContainsKey(card.Id)).All(card => card.Premium == 1), "fresh recipe deck takes the owned Golden");
        profile.CommitChange(data => { // explicit Normal fixture
            foreach (var card in data.CustomDecks.Single(row => row.Id == deck.Id).CardEntries.Where(card => BurnGolden.ContainsKey(card.Id))) card.Premium = 0;
            return true;
        });
        Check(profile.GetDraft(deck.Id).CardEntries.Where(card => BurnGolden.ContainsKey(card.Id)).All(card => card.Premium == 0), "fixture is Normal");
        var receipt = profile.Purchase("deck-burn-mage", 1, profile.PurchasePrice("deck-burn-mage"), "complete");
        Check(receipt.Granted.DeckId == deck.Id && profile.CustomDeckCount == 1, "completed in place");
        Check(profile.GetDraft(deck.Id).CardEntries.Where(card => BurnGolden.ContainsKey(card.Id)).All(card => card.Premium == 0),
            "in-place completion keeps explicit Normal choices although Golden is owned");
    }

    private static void ZeroCopyMastery()
    {
        const string id = "FIR_911";
        var data = new LocalProfileData();
        var tier = LocalCardMastery.Tiers(id, 0).First();
        int wins = (tier.Threshold + GrindTiers.Standard.MasteryPointsPerWin - 1) / GrindTiers.Standard.MasteryPointsPerWin;
        data.Matches = Enumerable.Range(0, wins).Select(_ => new LocalMatchRecord {
            Id = Guid.NewGuid().ToString(), Result = "Won", Turns = 4, GrindTier = GrindTiers.Standard.Id,
            AchievementContext = new LocalAchievementPendingContext { EligibleWorldMatch = true, PersonalDeck = true,
                MainDeck = new() { new LocalAchievementDeckCard { BaseCardId = id } } }
        }).ToList();
        Own(data, id, 1, 2);
        data.ClassRewardGrants.Add(new LocalClassRewardGrant { CardId = id, Premium = 1, Copies = 2 });
        var deck = new LocalCustomDeck { CardEntries = new() { Slot(id), Slot(id) } }; data.CustomDecks.Add(deck);
        Check(LocalCardMastery.Grant(data, null) && data.MasteryGrants.Single().Copies == 0, "zero-copy mastery fixture");
        Check(deck.CardEntries.All(card => card.Premium == 1), "zero-copy mastery still reconciles deck");
    }

    private static void SandboxMastery(string root)
    {
        const string mastered = "FIR_911";
        string path = TestPath(root, "deck-premium-sandbox-mastery");
        var options = new ProfileOptions { AllCosmetics = true };
        var choices = new[] { 1, 3, 2 }.Select(premium => LocalProfile.SupportedCheatPremium.First(row =>
            row.Premium == premium && row.Id != mastered && !LocalCoreProgression.Heroes.Contains(row.Id))).ToArray();
        var decks = new List<long>();
        using (var profile = new LocalProfile(path, options)) {
            profile.CommitChange(next => { foreach (var choice in choices) Own(next, choice.Id, 0, 1); return true; });
            foreach (var choice in choices) {
                var card = Cards.FromId(choice.Id);
                string hero = card.Class == SabberStoneCore.Enums.CardClass.NEUTRAL ? LocalDecks.PlayerHeroes[0]
                    : LocalDecks.PlayerHeroes.Single(id => Cards.FromId(id).Class == card.Class);
                var deck = profile.CreateDraft("Explicit " + choice.Premium, hero); decks.Add(deck.Id);
                profile.SavePremiumDraft(deck.Id, deck.Name, deck.Hero, new[] { Slot(choice.Id, choice.Premium) }, deck.SortOrder);
                Check(profile.StoredOwnedCount(choice.Id, choice.Premium) == 0 && profile.OwnedCount(choice.Id, choice.Premium) > 0,
                    "Sandbox choice is owned only through effective cosmetics policy");
            }
            profile.CommitChange(next => {
                var mastery = new LocalProfileData { CustomDecks = next.CustomDecks, Collection = next.Collection,
                    PremiumCollection = next.PremiumCollection,
                    Matches = Enumerable.Range(0, LocalCardMastery.Tiers(mastered, 0).First().RequiredWins).Select(_ =>
                        new LocalMatchRecord { Result = "Won", Turns = 4,
                            AchievementContext = new LocalAchievementPendingContext { PersonalDeck = true, EligibleWorldMatch = true,
                                MainDeck = new() { new LocalAchievementDeckCard { BaseCardId = mastered } } } }).ToList() };
                Check(LocalCardMastery.Grant(mastery, null), "unrelated mastery granted");
                next.MasteryGrants.AddRange(mastery.MasteryGrants);
                return true;
            });
            Check(decks.Select(id => profile.GetDraft(id).CardEntries.Single().Premium).SequenceEqual(choices.Select(row => row.Premium)),
                "mastery preserves unrelated explicit Golden/Signature/Diamond choices on Sandbox save");
        }
        using var reopened = new LocalProfile(path, options);
        Check(decks.Select(id => reopened.GetDraft(id).CardEntries.Single().Premium).SequenceEqual(choices.Select(row => row.Premium)),
            "Sandbox premium choices survive mastery persistence and reload");
    }

    private static void LoadRepair(string root)
    {
        string path = TestPath(root, "deck-premium-repair.xml");
        using (var profile = new LocalProfile(path)) {
            profile.GrantWelcomeIfNew();
            profile.CommitChange(data => { data.GoldBalance = 100000; return true; });
            profile.Purchase("deck-burn-mage", 1, profile.PurchasePrice("deck-burn-mage"), "repair-deck");
        }
        var data = Read(path); var deck = data.CustomDecks.Single();
        foreach (var row in BurnGolden) { Own(data, row.Key, 0, 0); Own(data, row.Key, 1, 2); }
        Check(LocalCrafting.MissingDeckSlots(deck, LocalProfile.DeckOwnedCount(data)) == 7, "synthetic owner shape has seven missing");
        Write(path, data); var collection = data.Collection.Concat(data.PremiumCollection).Select(card => card.Id + ":" + card.Premium + ":" + card.Count).ToArray();
        var logs = new List<string>(); var priorLog = LocalProfile.DeckRepairLog; LocalProfile.DeckRepairLog = logs.Add;
        try {
            using (var profile = new LocalProfile(path)) {
                Check(profile.MissingDeckSlotCount(deck.Id) == 0 && profile.IsDeckComplete(deck.Id), "load repairs seven slots");
                Check(profile.GetDraft(deck.Id).Cards.SequenceEqual(deck.Cards), "load preserves every card");
            }
            Check(logs.Count == 1 && logs[0].Contains(deck.Id.ToString()) && logs[0].Contains("0->1"), "one log per repaired deck");
            var bytes = File.ReadAllBytes(path);
            using (var profile = new LocalProfile(path)) Check(profile.MissingDeckSlotCount(deck.Id) == 0, "repair survives restart");
            Check(bytes.SequenceEqual(File.ReadAllBytes(path)) && logs.Count == 1, "reload is byte-idempotent and silent");
            Check(Read(path).Collection.Concat(Read(path).PremiumCollection).Select(card => card.Id + ":" + card.Premium + ":" + card.Count).SequenceEqual(collection),
                "repair grants/removes no copies");
        } finally { LocalProfile.DeckRepairLog = priorLog; }
    }

    private static void Disenchant(string root)
    {
        const string id = "FIR_911"; string path = TestPath(root, "deck-premium-disenchant.xml");
        using var profile = new LocalProfile(path);
        profile.GrantWelcomeIfNew();
        profile.CommitChange(data => { Own(data, id, 0, 1); Own(data, id, 1, 1); data.DustBalance = 10000; return true; });
        var deck = profile.CreateDraft("Finish switch", "HERO_08");
        profile.SaveDraft(deck.Id, deck.Name, deck.Hero, new[] { id }, deck.SortOrder);
        var ledger = new LocalCraftingLedger(profile);
        Check(ledger.CommitBatch(id, CraftDirection.Disenchant, 1, 1).Ok && profile.GetDraft(deck.Id).CardEntries.Single().Premium == 1,
            "disenchant last normal uses owned golden");
        Check(ledger.CommitBatch(id, CraftDirection.Craft, 1, 0).Ok, "craft replacement normal");
        Check(LocalCrafting.CommitPremiumBatch(profile, id, 1, CraftDirection.Disenchant, 1, 1).Ok
            && profile.GetDraft(deck.Id).CardEntries.Single().Premium == 0, "disenchant last golden uses owned normal");
    }

    private static void Purchase(string root, string recipeId)
    {
        string path = TestPath(root, "deck-premium-purchase-" + recipeId + ".xml");
        using var profile = new LocalProfile(path);
        profile.GrantWelcomeIfNew();
        profile.CommitChange(data => {
            data.GoldBalance = 100000; data.DustBalance = 100000;
            if (recipeId == "burn-mage") foreach (var row in BurnGolden) Own(data, row.Key, 1, 2);
            return true;
        });
        profile.Purchase("recipe-" + recipeId, 1, profile.PurchasePrice("recipe-" + recipeId), "recipe");
        var deck = profile.SaveDeckFromRecipe(recipeId, Recipe(recipeId));
        if (recipeId == "burn-mage") {
            Check(deck.CardEntries.Where(card => BurnGolden.ContainsKey(card.Id)).All(card => card.Premium == 1), "recipe assigns golden-only copies");
            string craft = deck.Cards.First(id => !BurnGolden.ContainsKey(id) && profile.OwnedCount(id) == 0 && LocalCrafting.TryGetPrice(id, out _, out _));
            Check(new LocalCraftingLedger(profile).CommitBatch(craft, CraftDirection.Craft, 1, 0).Ok, "partial craft");
        }
        profile.SaveDraftCosmetics(deck.Id, "Personal name", new LocalDeckCosmetics { Coin = 1, CardBack = 0, RandomCoinUseFavorite = false });
        profile.CommitChange(data => { data.CustomDecks.Single(row => row.Id == deck.Id).SortOrder = 17; return true; });
        int price = profile.PurchasePrice("deck-" + recipeId);
        if (recipeId == "burn-mage") {
            var original = File.ReadAllBytes(path); int gold = profile.Gold;
            profile.CommitFailure = stage => { if (stage == "replace") throw new IOException("injected in-place purchase failure"); };
            try {
                try { profile.Purchase("deck-" + recipeId, 1, price, "complete"); throw new Exception("failed purchase accepted"); }
                catch (IOException) { }
                Check(original.SequenceEqual(File.ReadAllBytes(path)) && profile.Gold == gold && profile.CustomDeckCount == 1,
                    "in-place completion failure preserves deck, ledger, receipt and gold");
            } finally { profile.CommitFailure = null; }
        }
        var receipt = profile.Purchase("deck-" + recipeId, 1, price, "complete");
        Check(receipt.Granted.DeckId == deck.Id && profile.CustomDeckCount == 1, "complete matching recipe in place " + recipeId);
        Check(receipt.GoldPaid == price && (recipeId != "burn-mage" || receipt.Granted.Cards.All(card => !BurnGolden.ContainsKey(card.CardId))),
            "only missing copies are charged/granted; owned Golden copies receive no Normal grants");
        var saved = profile.GetDraft(deck.Id);
        Check(saved.Name == "Personal name" && saved.SortOrder == 17 && saved.Coin == 1 && saved.CardBack == 0 && !saved.RandomCoinUseFavorite && profile.MissingDeckSlotCount(deck.Id) == 0 && profile.IsDeckComplete(deck.Id),
            "preserve metadata and assign every owned slot " + recipeId);
        var bytes = File.ReadAllBytes(path);
        Check(profile.Purchase("deck-" + recipeId, 1, price, "complete").Sequence == receipt.Sequence
            && bytes.SequenceEqual(File.ReadAllBytes(path)), "purchase replay idempotent");
        Check(profile.SaveDeckFromRecipe(recipeId, Recipe(recipeId)).Id == deck.Id && profile.CustomDeckCount == 1, "recipe reuse after ready-made");
        try { profile.Purchase("deck-" + recipeId, 1, profile.PurchasePrice("deck-" + recipeId), "again"); throw new Exception("re-buy accepted"); }
        catch (InvalidOperationException) { }
        Check(profile.CustomDeckCount == 1, "re-buy creates no duplicate");
        bytes = File.ReadAllBytes(path);
        try { profile.Purchase("recipe-" + recipeId, 1, profile.PurchasePrice("recipe-" + recipeId), "recipe-again"); throw new Exception("recipe re-buy accepted"); }
        catch (InvalidOperationException) { }
        Check(profile.CustomDeckCount == 1 && bytes.SequenceEqual(File.ReadAllBytes(path)), "recipe purchase after ready-made cannot duplicate or charge again");
    }

    private static void DuplicateChoices(string root)
    {
        foreach (bool modified in new[] { false, true }) {
            string path = TestPath(root, "deck-premium-choice-" + modified + ".xml");
            using var profile = new LocalProfile(path);
            profile.GrantWelcomeIfNew();
            profile.CommitChange(data => { data.GoldBalance = 100000; return true; });
            profile.Purchase("recipe-burn-mage", 1, profile.PurchasePrice("recipe-burn-mage"), "recipe");
            var first = profile.SaveDeckFromRecipe("burn-mage", Recipe("burn-mage"));
            var second = profile.CreateDraft("Other", first.Hero);
            profile.CommitChange(data => {
                var a = data.CustomDecks.Single(row => row.Id == first.Id);
                var b = data.CustomDecks.Single(row => row.Id == second.Id);
                b.Cards = first.Cards.ToList(); b.SourceRecipeId = first.SourceRecipeId; a.SortOrder = 10; b.SortOrder = 5;
                if (modified) { a.Cards.RemoveAt(0); b.Cards.RemoveAt(0); }
                return true;
            });
            var receipt = profile.Purchase("deck-burn-mage", 1, profile.PurchasePrice("deck-burn-mage"), "deck");
            Check(modified ? profile.CustomDeckCount == 3 && receipt.Granted.DeckId != first.Id && receipt.Granted.DeckId != second.Id
                : profile.CustomDeckCount == 2 && receipt.Granted.DeckId == second.Id, "modified composition/new deck or lowest SortOrder");
        }
    }

    // Runtime-only evidence path. Deserialize read-only, then exercise the real loader on an isolated disposable copy.
    internal static void OwnerSave(string evidencePath)
    {
        var original = File.ReadAllBytes(evidencePath);
        string root = Path.Combine(Path.GetTempPath(), "hs-owner-premium-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try {
            var before = Read(evidencePath);
            var deck = before.CustomDecks.Single(row => row.SourceRecipeId == "burn-mage");
            int missingBefore = LocalCrafting.MissingDeckSlots(deck, LocalProfile.DeckOwnedCount(before));
            string copy = Path.Combine(root, "profile.xml"); File.WriteAllBytes(copy, original);
            using var profile = new LocalProfile(copy);
            int missingAfter = profile.MissingDeckSlotCount(deck.Id);
            Console.WriteLine("OWNER_SAVE deck=" + deck.Id + " missing=" + missingBefore + "->" + missingAfter);
            Check(missingBefore == 7 && missingAfter == 0, "owner save seven missing repaired");
            Check(original.SequenceEqual(File.ReadAllBytes(evidencePath)), "owner evidence unchanged");
        } finally { Directory.Delete(root, true); }
    }
}
