using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using PegasusGame;

// Owner report (combined-24): changing a shop deck's card back and saving failed with
// "Unsupported local deck variant or sideboard.." The deck tray's card back, hero skin and random-hero
// toggle are per-deck cosmetics: they save with the cards, survive restart and dress the match.
internal static class DeckCosmeticsSaveTests
{
    private static void Check(bool ok, string label) { if (!ok) throw new Exception(label); }

    private static CollectionDeck Native(LocalCustomDeck saved, IEnumerable<string> ids)
    {
        var deck = new CollectionDeck { ID = saved.Id, Name = saved.Name, HeroCardID = saved.Hero,
            Type = PegasusShared.DeckType.NORMAL_DECK, FormatType = NativeUIProbe.LocalDeckFormat,
            SourceType = PegasusShared.DeckSourceType.DECK_SOURCE_TYPE_NORMAL, RandomPetUseFavorite = saved.RandomPetUseFavorite,
            PetID = saved.PetFamily == null ? (int?)null : LocalShop.Default.PetNativeId(saved.PetFamily),
            PetVariantID = saved.PetForm == null ? (int?)null : NativeMatch.NativePetVariant(saved.PetForm) };
        foreach (var group in ids.GroupBy(id => id))
        {
            var slot = new CollectionDeckSlot { CardID = group.Key };
            slot.SetCount(group.Count(), TAG_PREMIUM.NORMAL);
            deck.GetSlots().Add(slot);
        }
        deck.SetRuneOrder(NativeDeckRunes.Order(saved.Hero, ids));
        return deck;
    }

    // Mirrors native CollectionManager.AddDeck: the working and base decks are built from the created header.
    private static CollectionDeck FromHeader(NetCache.DeckHeader header) => new CollectionDeck {
        ID = header.ID, Type = header.Type, Name = header.Name, HeroCardID = header.Hero, HeroOverridden = header.HeroOverridden,
        CosmeticCoinID = header.CosmeticCoin, RandomCoinUseFavorite = header.RandomCoinUseFavorite, CardBackID = header.CardBack,
        SeasonId = header.SeasonId, BrawlLibraryItemId = header.BrawlLibraryItemId, NeedsName = header.NeedsName,
        SortOrder = header.SortOrder, FormatType = header.FormatType, SourceType = header.SourceType, Locked = header.Locked,
        UIHeroOverrideCardID = header.UIHeroOverride, UIHeroOverridePremium = header.UIHeroOverridePremium,
        RandomHeroUseFavorite = header.RandomHeroUseFavorite, PetID = header.Pet, PetVariantID = header.PetVariant,
        RandomPetUseFavorite = header.RandomPetUseFavorite };

    // Live 2026-10-25 (B1): on a newly created deck the card-back / hero-skin presses were lost. Create a deck the way
    // CreateLocalDraft does, let the trays write through their binding (null and stale bindings), save, and reload.
    private static void NewDeckSession(string path)
    {
        long id;
        string[] mage = LocalDecks.Get("mage").CardIds.ToArray();
        using (var profile = new LocalProfile(path))
        {
            var created = profile.CreateDraft("New Cosmetic Mage", "HERO_08");
            id = created.Id;
            var ledger = LocalProfile.LedgerDeck(created.Name, created.Hero, created.Cards, created.Sideboards, false);
            var header = NativeUIProbe.ExpectedHeader(id, ledger, created.Name, 7, profile.DeckCosmetics(id));
            Check(header.CardBack == null && !header.HeroOverridden && header.Hero == "HERO_08", "new deck starts on the favourites");
            var working = FromHeader(header);
            var prior = FromHeader(header);
            foreach (string card in mage)
            {
                var slot = working.GetSlots().FirstOrDefault(row => row.CardID == card);
                if (slot == null) { slot = new CollectionDeckSlot { CardID = card }; working.GetSlots().Add(slot); }
                slot.SetCount(slot.GetCount(TAG_PREMIUM.NORMAL) + 1, TAG_PREMIUM.NORMAL);
            }
            working.SetRuneOrder(NativeDeckRunes.Order("HERO_08", mage));
            prior.SetRuneOrder(NativeDeckRunes.Order("HERO_08", Array.Empty<string>()));

            // Card-back tray never bound (null m_currentDeck): bound to the edited deck, the press lands on it.
            CollectionDeck backTray = null;
            Check(NativeUIProbe.BindTrayDeck(() => backTray, deck => backTray = deck, working, true) && ReferenceEquals(backTray, working),
                "unbound card-back tray binds to the new edited deck");
            backTray.CardBackID = 122;
            // Hero-skin tray still holding another deck object: rebound, so the skin is not written to the old deck.
            var stale = FromHeader(header);
            CollectionDeck skinTray = stale;
            Check(NativeUIProbe.BindTrayDeck(() => skinTray, deck => skinTray = deck, working, true) && ReferenceEquals(skinTray, working),
                "stale hero-skin tray rebinds to the new edited deck");
            skinTray.HeroOverridden = true; skinTray.HeroCardID = "HERO_08a";
            Check(!stale.HeroOverridden && stale.CardBackID == null, "the stale deck object is untouched");
            // Already bound (existing deck sessions), loaners and unsaved decks are left alone.
            Check(!NativeUIProbe.BindTrayDeck(() => skinTray, deck => skinTray = null, working, true) && ReferenceEquals(skinTray, working),
                "a bound tray is left as is");
            CollectionDeck loanerTray = null;
            var loaner = new CollectionDeck { ID = NativeUIProjection.DeckId("warrior") };
            Check(!NativeUIProbe.BindTrayDeck(() => loanerTray, deck => loanerTray = deck, loaner, true) && loanerTray == null
                && !NativeUIProbe.BindTrayDeck(() => loanerTray, deck => loanerTray = deck, working, false) && loanerTray == null
                && !NativeUIProbe.BindTrayDeck(() => loanerTray, deck => loanerTray = deck, null, true),
                "loaner, unsaved or missing decks never bind a tray");

            // Done: SaveCurrentDeck -> SendChanges -> SaveLocalDraft's commit.
            var saved = NativeUIProbe.CommitLocalDraft(profile, working, prior);
            Check(saved.CardBack == 122 && saved.HeroSkin == "HERO_08a" && saved.Hero == "HERO_08" && saved.Cards.Count == mage.Length,
                "new deck saves its card back, hero skin and cards");
            var projected = new NetCache.DeckHeader();
            NativeUIProbe.ProjectSavedDraft(saved, working, prior, projected, 7);
            Check(projected.CardBack == 122 && projected.Hero == "HERO_08a" && projected.HeroOverridden
                && working.CardBackID == 122 && prior.HeroCardID == "HERO_08a", "new deck cosmetics project back to native");
        }
        using (var profile = new LocalProfile(path))
        {
            var reloaded = profile.GetDraft(id);
            Check(reloaded.CardBack == 122 && reloaded.HeroSkin == "HERO_08a" && profile.DeckCardBack(id, new Random(1)) == 122
                && profile.DeckHero(id, "HERO_08") == "HERO_08a", "new deck cosmetics survive restart and dress the match");
        }
    }

    private static void GlobalPetSession(string path)
    {
        const string family = "PET_10_1";
        var options = new ProfileOptions { AllCosmetics = true };
        var oldVariant = NativeMatch.NativePetVariant;
        var forms = LocalShop.Default.FormsForPet(family).ToArray();
        NativeMatch.NativePetVariant = card => 100 + Array.IndexOf(forms, card);
        long id, secondId;
        try
        {
            using (var profile = new LocalProfile(path, options))
            {
                profile.SelectPet(forms[0]);
                var deck = profile.CreateDraft("Pet Mage", "HERO_08"); id = deck.Id;
                var second = profile.CreateDraft("Other Mage", "HERO_08"); secondId = second.Id;
                string[] cards = LocalDecks.Get("mage").CardIds.ToArray();
                profile.SaveDraft(id, deck.Name, deck.Hero, cards, deck.SortOrder);
                var saved = profile.GetDraft(id);
                var working = Native(saved, cards); var prior = Native(saved, cards);
                working.PetID = LocalShop.Default.PetNativeId(family);
                working.PetVariantID = NativeMatch.NativePetVariant(forms.Last());
                working.RandomPetUseFavorite = false;
                var committed = NativeUIProbe.CommitLocalDraft(profile, working, prior);
                Check(committed.PetFamily == family && committed.PetForm == forms.Last(), "pet variant saved on personal deck");
                var header = new NetCache.DeckHeader();
                NativeUIProbe.ProjectSavedDraft(committed, working, prior, header, 5);
                Check(header.Pet == working.PetID && header.PetVariant == working.PetVariantID
                    && prior.PetID == working.PetID && prior.PetVariantID == working.PetVariantID, "pet projection retains override");
                Check(profile.DeckPet("custom:" + id, 1) == forms.Last()
                    && profile.DeckPet("custom:" + secondId, 1) == forms[0], "deck pet selection independent with favorite fallback");
                byte[] before = File.ReadAllBytes(path);
                working.PetVariantID = null;
                profile.CommitFailure = stage => { if (stage == "write") throw new IOException("deck save fault"); };
                bool failed = false;
                try { NativeUIProbe.CommitLocalDraft(profile, working, prior); } catch (IOException) { failed = true; }
                finally { profile.CommitFailure = null; }
                Check(failed && File.ReadAllBytes(path).SequenceEqual(before) && prior.PetVariantID.HasValue,
                    "failed pet save preserves bytes and base projection");
                working.PetVariantID = 999999;
                bool rejected = false;
                try { NativeUIProbe.CommitLocalDraft(profile, working, prior); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected && File.ReadAllBytes(path).SequenceEqual(before), "unknown pet variant rejected");
            }
            using (var profile = new LocalProfile(path, options))
            {
                Check(profile.GetDraft(id).PetForm == forms.Last() && !profile.GetDraft(id).RandomPetUseFavorite
                    && profile.DeckPet("custom:" + id, 1) == forms.Last(),
                    "pet override survives reopen");
                var saved = profile.GetDraft(id); var working = Native(saved, saved.Cards.ToArray()); var prior = Native(saved, saved.Cards.ToArray());
                working.PetID = null; working.PetVariantID = null; working.RandomPetUseFavorite = true;
                var removed = NativeUIProbe.CommitLocalDraft(profile, working, prior);
                Check(removed.PetFamily == null && profile.DeckPet("custom:" + id, 1) == forms[0], "removal restores favorite");
                working.RandomPetUseFavorite = false;
                var random = NativeUIProbe.CommitLocalDraft(profile, working, prior);
                var choices = Enumerable.Range(0, 50).Select(seed => profile.DeckPet("custom:" + id, seed)).Distinct().ToArray();
                Check(!random.RandomPetUseFavorite && choices.Length > 1 && choices.All(profile.CanUsePetForm), "random selects usable forms across families");
                NativeUIProbe.ProjectSavedDraft(random, working, prior, new NetCache.DeckHeader(), 5);
                working.PetID = LocalShop.Default.PetNativeId(family); working.RandomPetUseFavorite = true;
                var parent = NativeUIProbe.CommitLocalDraft(profile, working, prior);
                Check(parent.PetFamily == family && parent.PetForm == null && profile.DeckPet("custom:" + id, 2) == forms[0],
                    "family favorites selects favorite form");
            }
        }
        finally { NativeMatch.NativePetVariant = oldVariant; }
    }

    internal static void Run()
    {
        using var fixture = new CosmeticsFixture();
        var previousRandom = NativeUIProbe.RandomCardBackSource;
        string dir = Path.Combine(Path.GetTempPath(), "deck-cosmetics-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "profile.xml");
        try
        {
            NativeUIProbe.RandomCardBackSource = () => 9999;
            using (var p = new LocalProfile(path)) { p.GrantWelcomeIfNew(); p.SelectCardBack(0); }
            var serializer = new XmlSerializer(typeof(LocalProfileData));
            LocalProfileData data;
            using (var file = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(file);
            data.GoldBalance = 100000;
            using (var file = File.Create(path)) serializer.Serialize(file, data);
            string[] mage = LocalDecks.Get("mage").CardIds.ToArray();
            long customId, shopId;
            string[] shopCards;
            using (var profile = new LocalProfile(path))
            {
                profile.Purchase("heroskin-HERO_08a", 1, profile.PurchasePrice("heroskin-HERO_08a"), "skin");
                profile.Purchase("cardback-122", 1, profile.PurchasePrice("cardback-122"), "back");
                var deckProduct = LocalShop.Default.Product("deck-burn-mage");
                var receipt = profile.Purchase(deckProduct.Id, 1, profile.PurchasePrice(deckProduct.Id), "deck");
                shopId = receipt.Granted.DeckId.Value;
                var custom = profile.CreateDraft("Cosmetic Mage", "HERO_08");
                customId = custom.Id;
                Check(System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(path), "<LocalCustomDeck>[\\s\\S]*?</LocalCustomDeck>")
                    .Cast<System.Text.RegularExpressions.Match>().All(m => !m.Value.Contains("<CardBack") && !m.Value.Contains("<HeroSkin")
                        && !m.Value.Contains("<Random")), "decks without cosmetics keep the old save shape");

                // Hand-built deck: card back + hero skin + random-hero toggle.
                var prior = Native(custom, Array.Empty<string>());
                var working = Native(custom, mage);
                working.CardBackID = 122; working.HeroOverridden = true; working.HeroCardID = "HERO_08a"; working.RandomHeroUseFavorite = true;
                var saved = NativeUIProbe.CommitLocalDraft(profile, working, prior);
                Check(saved.Hero == "HERO_08" && saved.CardBack == 122 && saved.HeroSkin == "HERO_08a" && saved.RandomHeroUseFavorite
                    && saved.Cards.Count == mage.Length, "custom deck saves card back, hero skin and cards");
                var header = new NetCache.DeckHeader();
                NativeUIProbe.ProjectSavedDraft(saved, working, prior, header, 3);
                Check(header.CardBack == 122 && header.Hero == "HERO_08a" && header.HeroOverridden && header.HeroPower == "HERO_08bp"
                    && header.RandomHeroUseFavorite && header.FormatType == NativeUIProbe.LocalDeckFormat && prior.CardBackID == 122 && prior.HeroCardID == "HERO_08a", "saved cosmetics project to native");

                // Shop full deck: card back only (the reported case), then the native random-back marker.
                var shop = profile.GetDraft(shopId);
                shopCards = shop.Cards.ToArray();
                var shopPrior = Native(shop, shop.Cards);
                var shopWorking = Native(shop, shop.Cards);
                shopWorking.CardBackID = 122;
                saved = NativeUIProbe.CommitLocalDraft(profile, shopWorking, shopPrior);
                Check(saved.CardBack == 122 && saved.HeroSkin == null && saved.SourceRecipeId == shop.SourceRecipeId
                    && saved.Cards.OrderBy(id => id).SequenceEqual(shopCards.OrderBy(id => id)), "shop deck card back saves");
                shopPrior.CopyFrom(shopWorking);
                shopWorking.CardBackID = 9999;
                saved = NativeUIProbe.CommitLocalDraft(profile, shopWorking, shopPrior);
                Check(saved.CardBack == null && saved.RandomCardBack, "random card back saves as the random marker");
                NativeUIProbe.ProjectSavedDraft(saved, shopWorking, shopPrior, new NetCache.DeckHeader(), 4);
                Check(shopWorking.CardBackID == 9999, "random marker projects back to native");

                // Unowned card back and another class's skin are refused, leaving the saved deck unchanged.
                var bad = Native(profile.GetDraft(customId), mage);
                bad.CardBackID = 4242;
                bool refused = false;
                try { NativeUIProbe.CommitLocalDraft(profile, bad, bad); } catch (InvalidOperationException) { refused = true; }
                Check(refused && profile.GetDraft(customId).CardBack == 122, "unowned card back refused");
            }
            using (var profile = new LocalProfile(path))
            {
                var custom = profile.GetDraft(customId);
                Check(custom.CardBack == 122 && custom.HeroSkin == "HERO_08a" && custom.RandomHeroUseFavorite
                    && profile.GetDraft(shopId).RandomCardBack, "deck cosmetics survive restart");
                Check(profile.DeckCardBack(customId, new Random(1)) == 122 && profile.DeckHero(customId, "HERO_08") == "HERO_08a",
                    "match presentation uses the deck's own cosmetics");
                Check(profile.OwnedCardBacks.Contains(profile.DeckCardBack(shopId, new Random(1))), "random back draws an owned back");

                // Removing the skin (native keeps the skin id with HeroOverridden false) saves "no skin".
                var prior = Native(custom, custom.Cards);
                prior.HeroCardID = "HERO_08a"; prior.HeroOverridden = true; prior.CardBackID = 122; prior.RandomHeroUseFavorite = true;
                var working = Native(custom, custom.Cards);
                working.CopyFrom(prior);
                working.HeroOverridden = false; working.CardBackID = null;
                var saved = NativeUIProbe.CommitLocalDraft(profile, working, prior);
                Check(saved.HeroSkin == null && saved.CardBack == null && saved.Hero == "HERO_08", "removed skin and back save as favourites");
                NativeUIProbe.ProjectSavedDraft(saved, working, prior, new NetCache.DeckHeader(), 3);
                Check(working.HeroCardID == "HERO_08" && !working.HeroOverridden && prior.HeroCardID == "HERO_08",
                    "removed skin projects the class hero back");
                Check(profile.DeckHero(customId, "HERO_08") == profile.FavoriteHero("HERO_08")
                    && profile.DeckCardBack(customId, new Random(1)) == profile.FavoriteCardBacks.Single(), "favourites apply again");

                // Match: the deck's own back and skin dress the player.
                working.HeroOverridden = true; working.HeroCardID = "HERO_08a"; working.CardBackID = 122;
                NativeUIProbe.CommitLocalDraft(profile, working, prior);
                var deck = profile.GetDraft(customId);
                NativeMatch.CosmeticsProfile = () => profile;
                var packets = new List<(int, IProtoBuf)>();
                var match = new NativeMatch((id, message) => packets.Add((id, message)),
                    LocalProfile.LedgerDeck(deck.Name, deck.Hero, deck.Cards, deck.Sideboards, false), "warrior", 1337) { CosmeticDeckId = customId };
                match.Start();
                var histories = packets.Where(p => p.Item2 is PowerHistory).Select(p => (PowerHistory)p.Item2);
                var create = histories.SelectMany(h => h.List).Select(h => h.CreateGame).First(c => c != null);
                var hero = histories.SelectMany(h => h.List).Select(h => h.FullEntity).Where(e => e != null)
                    .First(e => e.Entity == match.Game.Player1.Hero.Id);
                Check(create.Players.Single(p => p.Id == 1).CardBack == 122 && hero.Name == "HERO_08a"
                    && match.Game.Player1.Hero.Card.Id == "HERO_08", "match uses the deck's card back and skin, engine keeps the class hero");
                profile.SetCardBackFavorite(122, true);
                (int back, int opponent, string fingerprint) FavoriteMatch() {
                    var messages = new List<PowerHistory>();
                    var game = new NativeMatch((id, message) => { if (message is PowerHistory history) messages.Add(history); }, "mage", "warrior", 1337);
                    game.Start();
                    var players = messages.SelectMany(history => history.List).Select(history => history.CreateGame).First(row => row != null).Players;
                    return (players.Single(player => player.Id == 1).CardBack, players.Single(player => player.Id == 2).CardBack, MatchRecorder.Fingerprint(game.Game));
                }
                var first = FavoriteMatch(); var repeat = FavoriteMatch();
                Check(first == repeat && first.back == profile.MatchCardBack(new Random(NativeMatch.CardBackSeed(1337))), "native global favourite uses match seed deterministically");
                var pairs = new HashSet<(int, int)>(Enumerable.Range(0, 200).Select(s => (new Random(s).Next(2), profile.MatchCardBack(new Random(NativeMatch.CardBackSeed(s))))));
                Check(pairs.Count == 4 && NativeMatch.CardBackSeed(1337) == NativeMatch.CardBackSeed(1337) && NativeMatch.CardBackSeed(1337) != 1337,
                    "card-back draw is independent of the pet's raw-seed draw");
                profile.SelectCardBack(122);
                var single = FavoriteMatch();
                Check(single.back == 122 && single.opponent == first.opponent && single.fingerprint == first.fingerprint,
                    "single favourite retains back, opponent presentation and engine replay state");
            }
            NewDeckSession(path);
            GlobalPetSession(Path.Combine(dir, "global-pet.xml"));
            DeckVersionSaveTests.Run();
            Check(NativeUIProjection.DeckWriteFailureMessage("save", 910000010, "Unsupported local deck variant or sideboard.")
                == "Local deck save failed for 910000010: Unsupported local deck variant or sideboard. Fix the deck and retry.",
                "deck error message has a single period");
            Console.WriteLine("PASS per-deck card back / hero skin save, restart and match cosmetics");
        }
        finally
        {
            NativeMatch.CosmeticsProfile = null;
            NativeUIProbe.RandomCardBackSource = previousRandom;
            Directory.Delete(dir, true);
        }
    }
}
