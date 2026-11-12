// Owner 2026-10-30, retail CollectionDeck.CanCardBeAddedAsOwned (254477): King of the Underbelly contraband needs owned
// copies like any sideboard card. Ready-made decks charge for and grant missing picks; losing a pick breaks the deck.
internal static class UnderbellyOwnershipTests
{
    private const string Recipe = "companion-hunter", King = "JAIL_831";

    internal static void Run(Action<bool, string> check, Func<string, int, string> fresh)
    {
        var recipe = LocalShop.Default.Recipe(Recipe);
        var picks = recipe.Sideboards.Single(board => board.Owner == King).Cards.Select(row => row.Id).ToArray();
        check(picks.Length == 3 && picks.All(id => !LocalCardCatalog.IsCore(id) && !LocalShop.GrantOnly(id)), "Underbelly recipe fixture");
        var product = LocalShop.Default.Products.Single(row => row.Kind == "Deck" && row.RecipeId == Recipe);
        string path = fresh("underbelly-ownership", 100000);
        using var p = new LocalProfile(path);
        var missing = LocalShop.Default.Library.MissingCopies(recipe, RecipeLibrary.OwnedCount(p.OwnedAnyVersionCollection));
        check(picks.Concat(new[] { King }).All(id => missing.Cards.Any(card => card.Id == id && card.Missing == 1 && card.Purchasable == 1)),
            "ready-made deck charges for the King and every missing contraband pick");
        int price = p.PurchasePrice(product.Id);
        var receipt = p.Purchase(product.Id, 1, price, "underbelly-buy");
        long deckId = receipt.Granted.DeckId!.Value;
        check(picks.Concat(new[] { King }).All(id => receipt.Granted.Cards.Any(card => card.CardId == id && card.CountAfter == 1)
            && p.OwnedCount(id) == 1), "purchase grants the King and its contraband picks");
        check(p.IsDeckComplete(deckId), "owned contraband completes the deck");
        var dust = new LocalCraftingLedger(p).CommitBatch(picks[0], CraftDirection.Disenchant, 1, 1);
        check(dust.Ok && p.OwnedCount(picks[0]) == 0 && !p.IsDeckComplete(deckId),
            "an unowned contraband pick leaves the deck incomplete (no virtual sideboard ownership)");
        // Live 2026-10-30: offline the native Underbelly subset (19499) is empty, so the engine rule supplies membership,
        // only where native has no data for a modelled owner.
        check(LocalDecks.UsesLocalSideboardSubset(King, 19499, 0) && !LocalDecks.UsesLocalSideboardSubset(King, 19499, 12)
            && !LocalDecks.UsesLocalSideboardSubset(King, 0, 0) && !LocalDecks.UsesLocalSideboardSubset("CS2_182", 19499, 0),
            "engine subset only for a modelled owner with an empty native subset");
        check(picks.All(id => LocalDecks.SideboardEligible(King, id)), "recipe contraband picks are engine-eligible");
        check(!LocalDecks.SideboardEligible(King, "CS2_182") && !LocalDecks.SideboardEligible(King, King)
            && !LocalDecks.SideboardEligible(King, "NO_SUCH_CARD") && !LocalDecks.SideboardEligible("CS2_182", picks[0]),
            "non-Beasts, Hunter cards, unknown cards and unmodelled owners are refused");
        var beatrixPick = LocalShop.Default.Recipe("pure-paladin").Sideboards.Single().Cards.Single().Id;
        check(LocalDecks.SideboardEligible("JAIL_397", beatrixPick) && !LocalDecks.SideboardEligible("JAIL_397", picks[0]),
            "Beatrix eligibility follows the engine rule");
        Console.WriteLine("PASS Underbelly contraband ownership: shop charges and grants picks, unowned pick blocks the deck");
    }
}
