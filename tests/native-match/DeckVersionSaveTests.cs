using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

// Owner 2026-10-29 (Steam Deck): on a New Save cheats save, the bought "Medivh Imbue Priest" failed Done after a card
// back / pet / hero skin change: "Local deck save failed for 910000015: The local collection does not own this deck
// premium." Native template filling (the shop projection) gives every card its best owned version, Golden on an
// all-cosmetics save, and gives Medivh's Fabled companions (TIME_890t/t2) the primary's version; restoring the saved
// versions put Medivh back to Normal but left the companions Golden, and the ledger never owned premium companions.
// These checks go through CommitLocalDraft (SaveLocalDraft's commit) for every personal deck kind on both save kinds.
internal static class DeckVersionSaveTests
{
    private const string Medivh = "TIME_890";
    private static readonly string[] Companions = { "TIME_890t", "TIME_890t2" };
    private static void Check(bool ok, string label) { if (!ok) throw new Exception(label); }

    private static CollectionDeck Shell(LocalCustomDeck saved) => new CollectionDeck { ID = saved.Id, Name = saved.Name,
        HeroCardID = saved.HeroSkin ?? saved.Hero, Type = PegasusShared.DeckType.NORMAL_DECK, FormatType = NativeUIProbe.LocalDeckFormat,
        SourceType = PegasusShared.DeckSourceType.DECK_SOURCE_TYPE_NORMAL, CardBackID = saved.CardBack,
        HeroOverridden = saved.HeroSkin != null, RandomHeroUseFavorite = saved.RandomHeroUseFavorite,
        PetID = saved.PetFamily == null ? (int?)null : LocalShop.Default.PetNativeId(saved.PetFamily),
        RandomPetUseFavorite = saved.RandomPetUseFavorite };

    // Mirrors CollectionDeck.FillFromTemplateDeck + the shop/crafting projection (ProjectMissingDraftCards): best owned
    // version first (Diamond, Signature, Golden, Normal) with missing copies as a Normal ghost slot; companions skipped,
    // then added with the primary slot's version and Owned flag; then the saved versions are restored where owned.
    private static CollectionDeck Project(LocalProfile profile, LocalCustomDeck saved, bool restore = true)
    {
        var deck = Shell(saved);
        var bundled = new HashSet<string>(saved.Cards.SelectMany(LocalDecks.FabledBundle), StringComparer.Ordinal);
        foreach (var group in saved.Cards.GroupBy(id => id).Where(group => !bundled.Contains(group.Key)))
        {
            int left = group.Count();
            var owned = new CollectionDeckSlot { CardID = group.Key, Owned = true };
            foreach (var premium in new[] { TAG_PREMIUM.DIAMOND, TAG_PREMIUM.SIGNATURE, TAG_PREMIUM.GOLDEN, TAG_PREMIUM.NORMAL })
            {
                int copies = Math.Min(left, profile.OwnedCount(group.Key, (int)premium));
                if (copies > 0) owned.SetCount(copies, premium);
                left -= copies;
            }
            if (owned.Count > 0) deck.GetSlots().Add(owned);
            if (left > 0) { var ghost = new CollectionDeckSlot { CardID = group.Key }; ghost.SetCount(left, TAG_PREMIUM.NORMAL); deck.GetSlots().Add(ghost); }
            foreach (string companion in LocalDecks.FabledBundle(group.Key).Where(saved.Cards.Contains))
            {
                var primary = deck.GetSlots().First(slot => slot.CardID == group.Key);
                var slot = new CollectionDeckSlot { CardID = companion, Owned = primary.Owned };
                slot.SetCount(1, (TAG_PREMIUM)Enumerable.Range(0, 4).Reverse().First(p => primary.GetCount((TAG_PREMIUM)p) > 0));
                deck.GetSlots().Add(slot);
            }
        }
        deck.SetRuneOrder(NativeDeckRunes.Order(saved.Hero, saved.Cards));
        Sideboards(deck, saved.Sideboards ?? new List<LocalSideboard>());
        if (restore) NativeUIProbe.RestoreDraftVersions(deck, saved.CardEntries, profile.OwnedCount);
        return deck;
    }

    private static string Versions(IEnumerable<LocalDeckCard> entries) =>
        string.Join(",", entries.Select(card => card.Premium + ":" + card.Id).OrderBy(key => key, StringComparer.Ordinal));

    private static bool Rejected(Action action, string message)
    {
        try { action(); return false; }
        catch (InvalidOperationException error) { return error.Message == message; }
    }

    private static void SetVersion(CollectionDeck deck, string id, TAG_PREMIUM version)
    {
        var slot = deck.GetSlots().Single(row => row.CardID == id);
        int count = slot.Count;
        slot.SetCount(count, version);
        foreach (var other in new[] { TAG_PREMIUM.NORMAL, TAG_PREMIUM.GOLDEN, TAG_PREMIUM.DIAMOND, TAG_PREMIUM.SIGNATURE }.Where(p => p != version))
            slot.SetCount(0, other);
    }

    private static LocalProfile Open(string path, ProfileOptions options) => new LocalProfile(path, options);

    // A save started the way the New Save wizard does (welcome gift; cheats in options), with gold for the shop.
    private static void NewSave(string path, ProfileOptions options)
    {
        using (var profile = Open(path, options)) profile.PersistNewProfile(options);
        if (options.InfiniteGold) return;
        var serializer = new XmlSerializer(typeof(LocalProfileData));
        LocalProfileData data;
        using (var file = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(file);
        data.GoldBalance = 200000;
        using (var file = File.Create(path)) serializer.Serialize(file, data);
    }

    private static LocalDeckCosmetics Cosmetics(LocalProfile profile, bool cheat, string hero)
    {
        if (!cheat)
        {
            var products = new List<string>();
            if (!profile.OwnedCardBacks.Contains(122)) products.Add("cardback-122");
            if (!profile.OwnedPets.Contains("PET_3_1")) products.Add("pet-PET_3_1");
            if (!profile.OwnedHeroSkins.Any(id => LocalShop.Default.IsHeroSkinForClass(hero, id))) products.Add("heroskin-" + hero + "a");
            foreach (string product in products) profile.Purchase(product, 1, profile.PurchasePrice(product), "cos-" + product);
        }
        int back = profile.OwnedCardBacks.First(id => id != 0 && !profile.FavoriteCardBacks.Contains(id));
        string skin = profile.OwnedHeroSkins.First(id => LocalShop.Default.IsHeroSkinForClass(hero, id));
        string pet = profile.OwnedPets.First();
        return new LocalDeckCosmetics { CardBack = back, HeroSkin = skin, PetFamily = pet };
    }

    private static LocalCustomDeck RecipeDeck(LocalProfile profile, string recipeId, string requestId)
    {
        profile.Purchase("recipe-" + recipeId, 1, profile.PurchasePrice("recipe-" + recipeId), requestId);
        var recipe = LocalShop.Default.Recipe(recipeId);
        return profile.SaveDeckFromRecipe(recipeId, new LocalCustomDeck { Name = recipe.Name, Hero = recipe.Hero,
            Cards = recipe.Cards.SelectMany(card => Enumerable.Repeat(card.Id, card.Count)).ToList(),
            Sideboards = recipe.Sideboards.Select(row => new LocalSideboard { Source = row.Owner,
                Cards = row.Cards.SelectMany(card => Enumerable.Repeat(card.Id, card.Count)).ToList() }).ToList() });
    }

    // Native SideboardDeck constructors need Unity's DefLoader; seed the ordinary slot state as the B18 checks do.
    private static void Sideboards(CollectionDeck parent, IEnumerable<LocalSideboard> rows)
    {
        foreach (var row in rows)
        {
            var side = (SideboardDeck)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SideboardDeck));
            var empty = new CollectionDeck();
            for (var type = typeof(CollectionDeck); type != null; type = type.BaseType)
                foreach (var field in type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly))
                    field.SetValue(side, field.GetValue(empty));
            typeof(SideboardDeck).GetField("<MainDeck>k__BackingField", System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic).SetValue(side, parent);
            foreach (var card in row.CardEntries)
            {
                var slot = new CollectionDeckSlot { CardID = card.Id, Owned = true }; slot.SetCount(1, (TAG_PREMIUM)card.Premium);
                side.GetSlots().Add(slot);
            }
            parent.GetAllSideboards().Add(row.Source, side);
        }
    }

    private static void Dress(CollectionDeck deck, LocalDeckCosmetics cosmetics)
    {
        deck.CardBackID = cosmetics.CardBack; deck.HeroOverridden = true; deck.HeroCardID = cosmetics.HeroSkin;
        deck.PetID = LocalShop.Default.PetNativeId(cosmetics.PetFamily); deck.RandomPetUseFavorite = false;
    }

    // One deck kind: cosmetics-only Done keeps every card version; a plain card edit (one copy out) saves the cards.
    private static void CosmeticsThenEdit(LocalProfile profile, long id, LocalDeckCosmetics cosmetics, string label)
    {
        var before = profile.GetDraft(id);
        var prior = Project(profile, before);
        var working = Project(profile, before);
        Check(Versions(NativeUIProbe.DeckEntries(working)) == Versions(before.CardEntries),
            label + ": projection shows the saved versions (Fabled companions follow their primary)");
        Dress(working, cosmetics);
        var saved = NativeUIProbe.CommitLocalDraft(profile, working, prior);
        Check(saved.CardBack == cosmetics.CardBack && saved.HeroSkin == cosmetics.HeroSkin && saved.PetFamily == cosmetics.PetFamily
            && saved.Name == before.Name && saved.SortOrder == before.SortOrder && saved.SourceRecipeId == before.SourceRecipeId
            && Versions(saved.CardEntries) == Versions(before.CardEntries), label + ": cosmetics save keeps cards and versions");
        // Native reprojects the saved draft into both decks (ProjectSavedDraft; CopyFrom needs Unity for sideboards).
        prior = Project(profile, saved); working = Project(profile, saved);
        Check(working.CardBackID == cosmetics.CardBack && working.HeroCardID == cosmetics.HeroSkin && working.HeroOverridden,
            label + ": saved cosmetics reproject");

        // Rename through the same commit (PocoSetDeckName) keeps the cards too.
        working.Name = before.Name + " II";
        saved = NativeUIProbe.CommitLocalDraft(profile, working, prior);
        Check(saved.Name == before.Name + " II" && Versions(saved.CardEntries) == Versions(before.CardEntries) && saved.CardBack == cosmetics.CardBack,
            label + ": rename keeps cards, versions and cosmetics");
        prior = Project(profile, saved); working = Project(profile, saved);

        var edited = working.GetSlots().First(slot => slot.Count > 0 && LocalDecks.FabledBundle(slot.CardID).Count == 0
            && !Companions.Contains(slot.CardID) && !LocalDecks.KnownBundleAndSideboardCards.Any(card => card.Id == slot.CardID)
            && !working.GetAllSideboards().ContainsKey(slot.CardID));
        string removed = edited.CardID;
        var version = Enumerable.Range(0, 4).Select(p => (TAG_PREMIUM)p).First(p => edited.GetCount(p) > 0);
        edited.SetCount(edited.GetCount(version) - 1, version);
        if (edited.Count == 0) working.GetSlots().Remove(edited);
        saved = NativeUIProbe.CommitLocalDraft(profile, working, prior);
        Check(saved.Cards.Count == before.Cards.Count - 1 && saved.Cards.Count(card => card == removed) == before.Cards.Count(card => card == removed) - 1
            && saved.CardBack == cosmetics.CardBack && saved.HeroSkin == cosmetics.HeroSkin, label + ": plain card edit saves");
    }

    private static void SaveKind(string path, bool cheat)
    {
        var options = cheat ? new ProfileOptions { AllCards = true, AllCosmetics = true, InfiniteGold = true } : new ProfileOptions();
        string kind = cheat ? "cheat save" : "normal save";
        NewSave(path, options);
        long bought, recipe, starter, custom;
        LocalDeckCosmetics cosmetics;
        using (var profile = Open(path, options))
        {
            cosmetics = Cosmetics(profile, cheat, "HERO_09");
            bought = profile.Purchase("deck-medivh-imbue-priest", 1, profile.PurchasePrice("deck-medivh-imbue-priest"), "medivh")
                .Granted.DeckId.Value;
            var shopDeck = profile.GetDraft(bought);
            Check(shopDeck.Cards.Contains(Medivh) && Companions.All(shopDeck.Cards.Contains), kind + ": bought Medivh deck holds the bundle");
            // A new deck takes the best owned versions: all Normal on the normal save, premium on the All cosmetics save.
            if (cheat) Check(shopDeck.CardEntries.All(card => card.Premium != 0), "cheat save: fresh bought deck gets the best versions");
            else Check(shopDeck.CardEntries.All(card => card.Premium == 0), "normal save: bought Medivh deck all Normal");
            // The owner's report started from a Normal Medivh deck: rebuild that fixture explicitly.
            profile.CommitChange(data => {
                var deck = data.CustomDecks.Single(row => row.Id == bought);
                foreach (var card in deck.CardEntries.Concat(deck.Sideboards.SelectMany(board => board.CardEntries))) card.Premium = 0;
                return true;
            });
            shopDeck = profile.GetDraft(bought);
            Check(shopDeck.CardEntries.All(card => card.Premium == 0), kind + ": fixture is an all-Normal Medivh deck");

            // The reported failure: shop projection of the bought deck, then a cosmetics-only Done.
            var stale = Project(profile, shopDeck, restore: false);
            string staleVersions = string.Join(",", new[] { Medivh }.Concat(Companions).Select(id => string.Join("/",
                Enumerable.Range(0, 4).Select(p => stale.GetSlots().Where(slot => slot.CardID == id).Sum(slot => slot.GetCount((TAG_PREMIUM)p))))));
            if (cheat) Check(Companions.All(id => stale.GetSlots().Single(slot => slot.CardID == id).GetCount(TAG_PREMIUM.NORMAL) == 0),
                "fixture: template filling gives the companions a premium version on a cheat save: " + staleVersions);
            if (cheat)
            {
                // The owner's session: Medivh restored to Normal, companions left in the template's version. A cosmetics-only
                // Done saves the deck tray and keeps the saved versions; so does one on an entirely unrestored projection
                // (best owned version everywhere), which must not silently upgrade the deck.
                var reported = Project(profile, shopDeck);
                var reportedPrior = Project(profile, shopDeck);
                foreach (var deck in new[] { reported, reportedPrior })
                    foreach (string id in Companions) SetVersion(deck, id, TAG_PREMIUM.SIGNATURE);
                Dress(reported, cosmetics);
                var kept = NativeUIProbe.CommitLocalDraft(profile, reported, reportedPrior);
                Check(kept.CardBack == cosmetics.CardBack && Versions(kept.CardEntries) == Versions(shopDeck.CardEntries),
                    "cheat save: reported Medivh cosmetics Done saves and keeps every card Normal");
                var upgraded = Project(profile, shopDeck, restore: false);
                var upgradedPrior = Project(profile, shopDeck, restore: false);
                upgraded.CardBackID = profile.OwnedCardBacks.First(back => back != 0 && back != cosmetics.CardBack);
                kept = NativeUIProbe.CommitLocalDraft(profile, upgraded, upgradedPrior);
                Check(kept.CardBack == upgraded.CardBackID && Versions(kept.CardEntries) == Versions(shopDeck.CardEntries),
                    "cheat save: cosmetics Done on a best-version projection never changes card versions");
            }
            CosmeticsThenEdit(profile, bought, cosmetics, kind + " bought deck");

            // Recipe decks bought separately and saved from the recipe: Rafaam (Fabled+, 40 cards, nine companions) and
            // Pure Paladin (a JAIL_397 sideboard).
            var rafaam = RecipeDeck(profile, "rafaam-warlock", "recipe-rafaam");
            recipe = rafaam.Id;
            CosmeticsThenEdit(profile, recipe, Cosmetics(profile, cheat, rafaam.Hero), kind + " Rafaam recipe deck");
            var paladin = RecipeDeck(profile, "pure-paladin", "recipe-paladin");
            Check(paladin.Sideboards.Count == 1, "fixture: Pure Paladin has a sideboard");
            CosmeticsThenEdit(profile, paladin.Id, Cosmetics(profile, cheat, paladin.Hero), kind + " sideboard recipe deck");
            Check(profile.GetDraft(paladin.Id).Sideboards.Single().Cards.SequenceEqual(paladin.Sideboards.Single().Cards),
                kind + ": sideboard survives cosmetics and card edits");
            // Sideboard version change with an unchanged card list: a real edit (full save), owned only on the cheat save.
            {
                var sideSaved = profile.GetDraft(paladin.Id);
                var sidePrior = Project(profile, sideSaved);
                var sideWorking = Project(profile, sideSaved);
                var sideSlot = sideWorking.GetAllSideboards()["JAIL_397"].GetSlots().Single();
                sideSlot.SetCount(1, TAG_PREMIUM.GOLDEN); sideSlot.SetCount(0, TAG_PREMIUM.NORMAL);
                if (cheat)
                {
                    var sideResult = NativeUIProbe.CommitLocalDraft(profile, sideWorking, sidePrior);
                    Check(sideResult.Sideboards.Single().CardEntries.Single().Premium == 1
                        && sideResult.Sideboards.Single().Cards.SequenceEqual(sideSaved.Sideboards.Single().Cards)
                        && Versions(sideResult.CardEntries) == Versions(sideSaved.CardEntries),
                        "cheat save: sideboard version change saves with the same cards");
                }
                else Check(Rejected(() => NativeUIProbe.CommitLocalDraft(profile, sideWorking, sidePrior), "The local collection does not own this deck premium.")
                    && Versions(profile.GetDraft(paladin.Id).Sideboards.Single().CardEntries) == Versions(sideSaved.Sideboards.Single().CardEntries),
                    "normal save: unowned Golden sideboard copy is refused");
            }

            // Starter-derived deck: a new deck filled with a Core starter's list (the native template path).
            var core = LocalDecks.PlayerCoreStarterDecks.First(deck => deck.Hero == "HERO_09");
            var created = profile.CreateDraft("Starter copy", core.Hero);
            var empty = Shell(created);
            var filled = Project(profile, new LocalCustomDeck { Id = created.Id, Name = created.Name, Hero = core.Hero, Cards = core.CardIds });
            NativeUIProbe.CommitLocalDraft(profile, filled, empty);
            starter = created.Id;
            Check(profile.GetDraft(starter).Cards.Count == core.CardIds.Count, kind + ": starter copy saved");
            CosmeticsThenEdit(profile, starter, cosmetics, kind + " starter deck");

            // Hand-built custom deck with the Medivh bundle in it.
            var hand = profile.CreateDraft("Hand Medivh", "HERO_09");
            var cards = shopDeck.Cards.ToList();
            var handFilled = Project(profile, new LocalCustomDeck { Id = hand.Id, Name = hand.Name, Hero = "HERO_09", Cards = cards });
            NativeUIProbe.CommitLocalDraft(profile, handFilled, Shell(hand));
            custom = hand.Id;
            CosmeticsThenEdit(profile, custom, cosmetics, kind + " custom deck");

            // Exact versions: Golden Medivh brings Golden companions (native AddCard); saving it needs an owned Golden Medivh.
            var medivh = profile.GetDraft(bought);
            var prior = Project(profile, medivh);
            var golden = Project(profile, medivh);
            foreach (string id in new[] { Medivh }.Concat(Companions)) SetVersion(golden, id, TAG_PREMIUM.GOLDEN);
            const string premiumRefusal = "The local collection does not own this deck premium.";
            if (cheat)
            {
                var saved = NativeUIProbe.CommitLocalDraft(profile, golden, prior);
                Check(new[] { Medivh }.Concat(Companions).All(id => saved.CardEntries.Single(card => card.Id == id).Premium == 1),
                    "cheat save: Golden Medivh saves with its Golden companions");
                prior = Project(profile, saved); golden = Project(profile, saved);
                Check(Versions(NativeUIProbe.DeckEntries(golden)) == Versions(saved.CardEntries), "Golden bundle projects back exactly");
                // A Golden companion beside a Normal primary is never owned.
                SetVersion(golden, Medivh, TAG_PREMIUM.NORMAL);
                Check(Rejected(() => NativeUIProbe.CommitLocalDraft(profile, golden, prior), premiumRefusal),
                    "cheat save: Golden companions of a Normal Medivh are refused");

                // Signature Medivh (the version the Deck projected) with Signature companions saves, and a cosmetics-only
                // Done afterwards keeps all three Signature; Golden companions of a Signature Medivh are refused.
                prior = Project(profile, profile.GetDraft(bought));
                var signature = Project(profile, profile.GetDraft(bought));
                foreach (string id in new[] { Medivh }.Concat(Companions)) SetVersion(signature, id, TAG_PREMIUM.SIGNATURE);
                saved = NativeUIProbe.CommitLocalDraft(profile, signature, prior);
                Check(new[] { Medivh }.Concat(Companions).All(id => saved.CardEntries.Single(card => card.Id == id).Premium == 3),
                    "cheat save: Signature Medivh saves with its Signature companions");
                prior = Project(profile, saved); signature = Project(profile, saved);
                Check(Versions(NativeUIProbe.DeckEntries(signature)) == Versions(saved.CardEntries), "Signature bundle projects back exactly");
                signature.CardBackID = profile.OwnedCardBacks.First(back => back != 0 && back != saved.CardBack);
                var dressed = NativeUIProbe.CommitLocalDraft(profile, signature, prior);
                Check(dressed.CardBack == signature.CardBackID && Versions(dressed.CardEntries) == Versions(saved.CardEntries),
                    "cheat save: cosmetics Done keeps the Signature bundle");
                prior = Project(profile, dressed); signature = Project(profile, dressed);
                foreach (string id in Companions) SetVersion(signature, id, TAG_PREMIUM.GOLDEN);
                Check(Rejected(() => NativeUIProbe.CommitLocalDraft(profile, signature, prior), premiumRefusal),
                    "cheat save: Golden companions of a Signature Medivh are refused");
            }
            else
            {
                Check(Rejected(() => NativeUIProbe.CommitLocalDraft(profile, golden, prior), premiumRefusal),
                    "normal save: unowned Golden Medivh bundle is refused");
                SetVersion(golden, Medivh, TAG_PREMIUM.NORMAL);
                Check(Rejected(() => NativeUIProbe.CommitLocalDraft(profile, golden, prior), premiumRefusal),
                    "normal save: Golden companions alone are refused");
                var other = Project(profile, medivh);
                SetVersion(other, other.GetSlots().First(slot => slot.CardID != Medivh && !Companions.Contains(slot.CardID)).CardID, TAG_PREMIUM.GOLDEN);
                Check(Rejected(() => NativeUIProbe.CommitLocalDraft(profile, other, prior), premiumRefusal),
                    "normal save: an unowned Golden copy is refused");
                Check(Versions(profile.GetDraft(bought).CardEntries) == Versions(medivh.CardEntries), "refusals leave the deck unchanged");
            }
        }
        using (var profile = Open(path, options))
        {
            foreach (long id in new[] { recipe, starter, custom })
                Check(profile.GetDraft(id).CardBack.HasValue && profile.GetDraft(id).HeroSkin != null && profile.GetDraft(id).PetFamily != null,
                    kind + ": deck cosmetics survive restart");
            Check(profile.GetDraft(bought).PetFamily == cosmetics.PetFamily, kind + ": bought deck pet survives restart");
        }
    }

    // A saved Golden copy the player no longer owns (disenchant never edits deck entries). Since e81a1caca the load-time
    // RepairDeckPremiums rewrites such a slot to an owned version (DECK_PREMIUM_REPAIR, persisted), so the deck is complete
    // before native ever sees it, and a cosmetics-only Done keeps the repaired versions. A card owned in no version at all
    // stays an incomplete slot: the repair never removes cards and never grants copies.
    private static void StalePremiumRepair(string path)
    {
        var options = new ProfileOptions { AllCards = true };
        NewSave(path, options);
        long id; string stale, gone;
        using (var profile = Open(path, options))
        {
            var draft = profile.CreateDraft("Stale Golden", "HERO_08");
            profile.SaveDraft(draft.Id, draft.Name, draft.Hero, LocalDecks.Get("mage").CardIds.ToArray(), draft.SortOrder);
            id = draft.Id;
            stale = profile.GetDraft(id).Cards.First();
            Check(profile.IsDeckComplete(id) && profile.OwnedCount(stale, 1) == 0, "fixture: complete deck, no Golden copy owned");
        }
        var serializer = new XmlSerializer(typeof(LocalProfileData));
        LocalProfileData data;
        using (var file = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(file);
        var corrupt = data.CustomDecks.Single(deck => deck.Id == id);
        corrupt.CardEntries.First(card => card.Id == stale).Premium = 1;
        // A second card owned in no version: its Normal row is gone and no Core grant or premium copy covers it.
        gone = corrupt.Cards.Distinct().First(card => card != stale && data.Collection.Any(row => row.Id == card)
            && !data.CoreGrants.Any(row => row.Id == card) && !data.PremiumCollection.Any(row => row.Id == card));
        var goneRows = data.Collection.Where(row => row.Id == gone).ToList();
        data.Collection.RemoveAll(row => row.Id == gone);
        corrupt.CardEntries.First(card => card.Id == gone).Premium = 1;
        using (var file = File.Create(path)) serializer.Serialize(file, data);
        string Owned(LocalProfileData row) => string.Join(",", row.Collection.Concat(row.PremiumCollection)
            .Select(card => card.Id + ":" + card.Premium + ":" + card.Count).OrderBy(key => key, StringComparer.Ordinal));
        string collection = Owned(data);
        LocalProfileData Read() { using var file = File.OpenRead(path); return (LocalProfileData)serializer.Deserialize(file); }

        var logs = new List<string>(); var priorLog = LocalProfile.DeckRepairLog; LocalProfile.DeckRepairLog = logs.Add;
        try
        {
            using (var profile = Open(path, options))
            {
                var saved = profile.GetDraft(id);
                Check(logs.Count == 1 && logs[0].StartsWith("DECK_PREMIUM_REPAIR id=" + id + " ") && logs[0].EndsWith("cards=" + stale + ":1->0"),
                    "load logs exactly the stale Golden slot repair: " + string.Join(" | ", logs));
                Check(saved.CardEntries.Where(card => card.Id == stale).All(card => card.Premium == 0),
                    "load repairs the stale Golden slot to the owned Normal");
                Check(saved.CardEntries.Count(card => card.Id == gone && card.Premium == 1) == 1 && profile.OwnedCount(gone) == 0
                    && profile.OwnedCount(gone, 1) == 0, "a card owned in no version keeps its saved slot and is not granted");
                Check(!profile.IsDeckComplete(id) && profile.MissingDeckSlotCount(id) == corrupt.Cards.Count(card => card == gone),
                    "only the card owned in no version leaves the deck incomplete");
            }
            var persisted = Read();
            Check(persisted.CustomDecks.Single(deck => deck.Id == id).CardEntries.Where(card => card.Id == stale).All(card => card.Premium == 0),
                "the load repair is persisted");
            Check(Owned(persisted) == collection, "the repair grants and removes no copies");

            // Owning the card again (any version) completes the deck; the stale Golden repair stays as saved.
            persisted.Collection.AddRange(goneRows);
            using (var file = File.Create(path)) serializer.Serialize(file, persisted);
            logs.Clear();
            using (var profile = Open(path, options))
            {
                var saved = profile.GetDraft(id);
                Check(logs.Count == 1 && logs[0].EndsWith("cards=" + gone + ":1->0"), "regained Normal copies repair the other slot");
                Check(profile.IsDeckComplete(id) && profile.SavedVersionsOwned(id), "with every card owned the deck is complete");
                var cosmetics = Cosmetics(profile, false, "HERO_08");
                var prior = Project(profile, saved);
                var working = Project(profile, saved);
                Check(Versions(NativeUIProbe.DeckEntries(working)) == Versions(saved.CardEntries), "native projects the repaired versions back exactly");
                Dress(working, cosmetics);
                var dressed = NativeUIProbe.CommitLocalDraft(profile, working, prior);
                Check(Versions(dressed.CardEntries) == Versions(saved.CardEntries) && dressed.CardBack == cosmetics.CardBack && profile.IsDeckComplete(id),
                    "cosmetics Done keeps the repaired versions and the deck stays complete");
            }
            logs.Clear();
            var bytes = File.ReadAllBytes(path);
            using (var profile = Open(path, options))
                Check(profile.IsDeckComplete(id) && profile.GetDraft(id).CardBack.HasValue, "repair survives restart");
            Check(logs.Count == 0 && bytes.SequenceEqual(File.ReadAllBytes(path)), "reload after the repair is silent and byte-idempotent");
        }
        finally { LocalProfile.DeckRepairLog = priorLog; }
    }

    internal static void Run()
    {
        using var fixture = new CosmeticsFixture();
        string dir = Path.Combine(Path.GetTempPath(), "deck-versions-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        var previousRandom = NativeUIProbe.RandomCardBackSource;
        try
        {
            NativeUIProbe.RandomCardBackSource = () => 9999;
            foreach (bool cheat in new[] { false, true })
            {
                string save = Path.Combine(dir, cheat ? "cheat" : "normal");
                Directory.CreateDirectory(save);
                SaveKind(Path.Combine(save, "profile.xml"), cheat);
            }
            Directory.CreateDirectory(Path.Combine(dir, "stale"));
            StalePremiumRepair(Path.Combine(dir, "stale", "profile.xml"));
            Console.WriteLine("PASS deck saves keep card versions: bought, recipe, starter and custom decks on normal and cheat saves");
        }
        finally
        {
            NativeUIProbe.RandomCardBackSource = previousRandom;
            Directory.Delete(dir, true);
        }
    }
}
