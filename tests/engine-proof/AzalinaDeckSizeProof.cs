using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

internal static class AzalinaDeckSizeProof
{
    [GroupProof("azalina-deck-size")]
    internal static void Run()
    {
        foreach (var row in new[] { ("JAIL_430", 20), ("CS2_182", 30), ("TIME_005", 40) })
            Core2026Proof.Check(FabledRules.DeckSize(new[] { row.Item1 }) == row.Item2, "tag-driven deck size " + row.Item1);
        // Prince Renathal (40) is outside the engine universe; Rafaam is the in-pool 40-card rule.
        Core2026Proof.Check(FabledRules.DeckErrors(FabledRules.ExpandForDeckbuilding(new[] { "JAIL_430", "TIME_005" }))
            .Any(error => error.Contains("different limits")), "conflicting size rules");
        Core2026Proof.Check(!FabledRules.DeckErrors(FabledRules.ExpandForDeckbuilding(new[] { "TIME_005" })).Any(),
            "one size rule has no conflict");
        Core2026Proof.Check(!FabledRules.DeckErrors(new[] { "NOT_A_CARD" }).Any() && FabledRules.DeckSize(new[] { "NOT_A_CARD" }) == 30,
            "size selection leaves unknown definitions to caller validation");

        var modifiers = Cards.All.Where(card => card.Collectible && card[(GameTag)997] > 1)
            .ToDictionary(card => card.Id, card => card[(GameTag)997]);
        Core2026Proof.Check(LocalDecks.VendorDeckSizes.OrderBy(row => row.Key).SequenceEqual(modifiers.OrderBy(row => row.Key)),
            "vendor fallback exactly matches collectible tag-997 modifiers");

        var twenty = LocalDecks.Get("test-standard-azalina-priest").Build();
        var ordinary = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList();
        foreach (bool azalina in new[] { true, false })
        {
            var game = new Game(new GameConfig { StartPlayer = 1, RandomSeed = 19, Shuffle = false,
                SkipMulligan = false, Logging = false, Player1HeroClass = CardClass.PRIEST,
                Player2HeroClass = CardClass.MAGE, Player1Deck = azalina ? twenty : ordinary, Player2Deck = ordinary });
            game.StartGame();
            Core2026Proof.Check(game.Player1.Choice != null && game.Player2.Choice != null, "opening mulligan reached");
            Core2026Proof.Check(game.Player1.DeckZone.Count + game.Player1.HandZone.Count == (azalina ? 40 : 30)
                && game.Player1.DeckCards.Count == (azalina ? 20 : 30), "submitted size and physical opening total");
            // The second player's Coin is generated separately from the submitted thirty cards.
            Core2026Proof.Check(game.Player2.DeckZone.Count + game.Player2.HandZone.Count(card => card.Card.Id != "GAME_005") == 30,
                "opponent retains thirty original cards");
            Core2026Proof.Check(game.Player1.Hero.Health == (azalina ? 40 : 30) && game.Player1.Hero.BaseHealth == (azalina ? 40 : 30)
                && game.Player2.Hero.Health == 30 && game.Player2.Hero.BaseHealth == 30, "opening current and maximum Health");
        }
        const string code = "AAECAefwBAT8qQeAqgeEqgfn2AcI8J8EoPsGw/8GhYYHna0Hr8kHqtkHrNkHAAA=";
        var decoded = DeckSerializer.Deserialize(code);
        Core2026Proof.Check(decoded.CardDbfIds.Values.Sum() == 20 && decoded.GetCards().Any(row => row.Key.Id == "JAIL_430"),
            "retail twenty-card deck code decodes Azalina");
        // The retail code names Ice Lady Tyrande (HERO_09o), which is outside the engine universe.
        decoded.HeroDbfId = Cards.FromId("HERO_09").AssetId;
        var roundTrip = DeckSerializer.Deserialize(DeckSerializer.Serialize(decoded, false));
        Core2026Proof.Check(decoded.CardDbfIds.OrderBy(row => row.Key).SequenceEqual(roundTrip.CardDbfIds.OrderBy(row => row.Key))
            && roundTrip.HeroDbfId == decoded.HeroDbfId && roundTrip.Format == decoded.Format, "deck code round trip stable");
        Console.WriteLine("PASS Azalina deck-size rules, conflicts, opening setup and deck code");
    }
}
