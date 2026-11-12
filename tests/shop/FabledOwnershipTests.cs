using System.Xml.Serialization;

// Owner 2026-10-30: no virtual ownership. Fabled primaries and sideboard owners are ordinary collectible legendaries
// (ledger only, sold in ready-made decks, craftable and disenchantable); companions follow their primary.
internal static class FabledOwnershipTests
{
    private const string Rafaam = "TIME_005", RafaamRecipe = "rafaam-warlock";
    private static readonly XmlSerializer Xml = new XmlSerializer(typeof(LocalProfileData));

    private static void Edit(string path, Action<LocalProfileData> change)
    {
        LocalProfileData data;
        using (var file = File.OpenRead(path)) data = (LocalProfileData)Xml.Deserialize(file)!;
        change(data);
        using (var file = File.Create(path)) Xml.Serialize(file, data);
    }

    internal static void Run(Action<bool, string> check, Action<Action, string> reject, Func<string, int, string> fresh)
    {
        var companions = SabberStoneCore.Actions.FabledRules.Bundle(Rafaam);
        check(companions.Count == 9 && companions.All(LocalDecks.IsFabledCompanion) && !LocalDecks.IsFabledCompanion(Rafaam),
            "Rafaam fixture bundle");
        var product = LocalShop.Default.Products.Single(row => row.Kind == "Deck" && row.RecipeId == RafaamRecipe);

        string unowned = fresh("fabled-unowned", 100000), owned = fresh("fabled-owned", 100000);
        Edit(owned, data => data.Collection.Add(new LocalCardCount { Id = Rafaam, Count = 1, Acquired = 1 }));
        int ownedPrice;
        using (var p = new LocalProfile(owned)) ownedPrice = p.PurchasePrice(product.Id);

        long deckId;
        using (var p = new LocalProfile(unowned))
        {
            foreach (var card in LocalDecks.KnownBundleAndSideboardCards)
                check(!p.Collection.ContainsKey(card.Id) && p.OwnedCount(card.Id) == 0, "no free Fabled/sideboard ownership: " + card.Id);
            var missing = LocalShop.Default.Library.MissingCopies(LocalShop.Default.Recipe(RafaamRecipe),
                RecipeLibrary.OwnedCount(p.OwnedAnyVersionCollection));
            check(missing.Cards.Any(card => card.Id == Rafaam && card.Missing == 1 && card.Purchasable == 1)
                && !missing.Cards.Any(card => LocalDecks.IsFabledCompanion(card.Id)),
                "ready-made deck lists the missing Fabled primary, never its companions");
            int price = p.PurchasePrice(product.Id);
            check(price - ownedPrice == LocalShop.CopyPrice(SabberStoneCore.Enums.Rarity.LEGENDARY),
                "ready-made deck charges one legendary copy for the missing Fabled primary");
            var receipt = p.Purchase(product.Id, 1, price, "fabled-buy");
            deckId = receipt.Granted.DeckId!.Value;
            check(receipt.Granted.Cards.Any(card => card.CardId == Rafaam && card.CountBefore == 0 && card.CountAfter == 1)
                && !receipt.Granted.Cards.Any(card => LocalDecks.IsFabledCompanion(card.CardId)),
                "purchase grants the Fabled primary and no companion copies");
            check(p.Collection.TryGetValue(Rafaam, out int copies) && copies == 1 && companions.All(id => !p.Collection.ContainsKey(id)),
                "companions stay outside the collection");
            check(p.IsDeckComplete(deckId), "companions are owned through the purchased primary");
        }

        // Disenchanting the primary takes its companions with it; crafting it back restores the deck.
        Edit(unowned, data => data.DustBalance = 0);
        using (var p = new LocalProfile(unowned))
        {
            var ledger = new LocalCraftingLedger(p);
            var dust = ledger.CommitBatch(Rafaam, CraftDirection.Disenchant, 1, 1);
            check(dust.Ok && dust.DustDelta == 800 && p.DustBalance == 800 && p.OwnedCount(Rafaam) == 0,
                "a Fabled legendary disenchants for legendary dust");
            check(!p.IsDeckComplete(deckId), "companions without an owned primary leave the deck incomplete");
            check(!ledger.CommitBatch(Rafaam, CraftDirection.Craft, 1, 0).Ok && p.OwnedCount(Rafaam) == 0,
                "crafting needs the legendary dust");
        }
        Edit(unowned, data => data.DustBalance = 1600);
        using (var p = new LocalProfile(unowned))
        {
            var ledger = new LocalCraftingLedger(p);
            var craft = ledger.CommitBatch(Rafaam, CraftDirection.Craft, 1, 0);
            check(craft.Ok && craft.DustDelta == -1600 && p.DustBalance == 0 && p.OwnedCount(Rafaam) == 1
                && companions.All(id => !p.Collection.ContainsKey(id)), "a Fabled legendary crafts at the legendary price");
            check(p.IsDeckComplete(deckId), "a crafted primary owns its companions again");
            var saved = p.GetDraft(deckId);
            var resolved = LocalProfile.PlayableDeck(saved.Name, saved.Hero, saved.Cards, saved.Sideboards);
            NativeUIProjection.ValidateCustomStart(p, deckId, saved, resolved); // throws unless companions are credited
            check(companions.All(id => !NativeUIProjection.TotalOwned(p).ContainsKey(id)), "start check credits companions without owning them");
            foreach (string companion in companions)
                check(!LocalCrafting.TryGetPrice(companion, out _, out _), "companion is not craftable: " + companion);
        }
        // A golden-only primary (class level reward) owns Golden companions; a Golden companion without it does not.
        var recipe = LocalShop.Default.Recipe(RafaamRecipe);
        string golden = fresh("fabled-golden", 0);
        Edit(golden, data => {
            data.PremiumCollection.Add(new LocalCardCount { Id = Rafaam, Premium = 1, Count = 1, Acquired = 1 });
            foreach (var row in recipe.Cards.Where(row => row.Id != Rafaam && !companions.Contains(row.Id)))
            {
                var ledger = LocalCardCatalog.IsCore(row.Id) ? data.CoreGrants : data.Collection;
                var owned = ledger.SingleOrDefault(card => card.Id == row.Id);
                if (owned == null) ledger.Add(owned = new LocalCardCount { Id = row.Id });
                owned.Count = Math.Max(owned.Count, row.Count);
                if (!LocalCardCatalog.IsCore(row.Id)) owned.Acquired = Math.Max(owned.Acquired, row.Count);
            }
        });
        var entries = recipe.Cards.SelectMany(row => Enumerable.Repeat(new LocalDeckCard { Id = row.Id,
            Premium = row.Id == Rafaam || companions.Contains(row.Id) ? 1 : 0 }, row.Count)).ToList();
        long goldenId;
        using (var p = new LocalProfile(golden))
        {
            var draft = p.CreateDraft("Golden Rafaam", recipe.Hero);
            goldenId = draft.Id;
            p.SavePremiumDraft(goldenId, draft.Name, recipe.Hero, entries.Select(e => new LocalDeckCard { Id = e.Id, Premium = e.Premium }), 0);
            check(p.OwnedCount(Rafaam) == 0 && p.OwnedCount(Rafaam, 1) == 1 && p.IsDeckComplete(goldenId),
                "a golden-only Fabled primary completes its Golden bundle");
            var saved = p.GetDraft(goldenId);
            NativeUIProjection.ValidateCustomStart(p, goldenId, saved, LocalProfile.PlayableDeck(saved.Name, saved.Hero, saved.Cards, saved.Sideboards));
        }
        Edit(golden, data => data.PremiumCollection.RemoveAll(card => card.Id == Rafaam));
        using (var p = new LocalProfile(golden))
            check(!p.IsDeckComplete(goldenId), "Golden companions without an owned primary are not owned");
        foreach (string id in new[] { "JAIL_397", "JAIL_831" })
            check(LocalCrafting.TryGetPrice(id, out int buy, out int sell) && buy == 1600 && sell == 800,
                "sideboard owner crafts like any legendary: " + id);
        Console.WriteLine("PASS Fabled/sideboard ownership: ledger only, deck purchase, crafting, companions follow the primary");
    }
}
