using SabberStoneCore.Model;

internal static class CatalogueIndexTests
{
    internal static void Run(Action<bool, string> check)
    {
        // Independent linear predicates preserve the pre-index admission and ownership boundaries.
        var all = Cards.All.ToArray();
        var standard = FairPackRules.Eligible(FairPackRules.Standard, FairPackRules.RuleVersion);
        var extras = LocalCardCatalog.ActiveMetaExtraCards;
        bool IsStandard(SabberStoneCore.Model.Card card) => card.Collectible && card.Implemented
            && (LocalCardCatalog.IsCore(card.Id) || standard.Any(eligible => eligible.Set == card.Set) || extras.Contains(card.Id));
        var ledger = LocalCardCatalog.Current.Concat(standard)
            .Concat(OfflinePoolPolicy.Load().Members.Where(row => row.Scope == "Standard").Select(row => row.CardId)
                .Concat(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds).Select(Cards.FromId).Where(IsStandard))
            .Concat(all.Where(card => extras.Contains(card.Id) && IsStandard(card)))
            .Concat(SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds.SelectMany(SabberStoneCore.Actions.FabledRules.Bundle)
                .Select(Cards.FromId).Where(card => card != null && !LocalCardCatalog.IsCore(card.Id)))
            .Select(card => card.Id).ToHashSet(StringComparer.Ordinal);
        var bundleCards = SabberStoneCore.CardSets.Client.StandardSets.CollectibleIds
            .Where(id => SabberStoneCore.Actions.FabledRules.IsFabled(id) || SabberStoneCore.Actions.DeckbuildingRules.HasSideboard(id))
            .SelectMany(id => new[] { id }.Concat(SabberStoneCore.Actions.FabledRules.Bundle(id)))
            .Select(Cards.FromId).Where(card => card != null && card.Implemented).ToArray();
        var legal = bundleCards.Concat(all.Where(card => card.Collectible && card.Implemented
            && OfflinePoolPolicy.Load().Metas.Any(meta => meta.ExtraCards.Contains(card.Id))))
            .Where(LocalCardCatalog.InActiveMeta).ToArray();
        foreach (var card in all) {
            check(LocalShop.IsStandardCard(card) == IsStandard(card), "Standard admission unchanged: " + card.Id);
            check(LocalProfile.KnownLedgerCard(card.Id) == (ledger.Contains(card.Id) || LocalCoreProgression.AvailableCards.Any(c => c.Id == card.Id)),
                "Ledger/Core admission unchanged: " + card.Id);
            check(LocalDecks.IsCustomCard(card.Id) == LocalCardCatalog.Supported.Concat(legal).Any(c => c.Id == card.Id), "Custom admission unchanged: " + card.Id);
            check(LocalDecks.IsFabledCompanion(card.Id) == SabberStoneCore.Actions.FabledRules.IsCompanion(card.Id), "Companion index unchanged: " + card.Id);
            check(LocalDecks.IsLegalDeckbuildingCard(card.Id) == legal.Any(c => c.Id == card.Id), "Legal admission unchanged: " + card.Id);
        }
        check(!LocalProfile.KnownLedgerCard(null) && !LocalProfile.KnownLedgerCard("NO_SUCH_CARD") && !LocalShop.IsStandardCard(null), "Unknown catalogue entries stay rejected");
        var shop = LocalShop.Default;
        foreach (var product in shop.Products) {
            check(shop.Product(product.Id).Kind == product.Kind, "Product lookup unchanged");
            if (product.Kind == "HeroSkin") check(shop.IsHeroSkinForClass(product.Class, product.HeroSkin)
                && !shop.IsHeroSkinForClass("HERO_99", product.HeroSkin), "Skin class boundary unchanged");
        }
        var detached = shop.Product("pack-standard"); detached.Price = -1;
        check(shop.Product("pack-standard").Price == 100, "Product index cannot leak mutable catalogue entries");
        var recipe = shop.Recipe("manastorm-mage"); recipe.Cards.Clear();
        check(shop.Recipe("manastorm-mage").Cards.Sum(row => row.Count) == 30, "Recipe index cannot leak mutable catalogue entries");
        Console.WriteLine("PASS catalogue indexes: linear admission equivalence, companion index, cosmetics and detached snapshots");
    }
}
