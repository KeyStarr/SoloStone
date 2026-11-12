// Exercise the production bundle/section builders; only client DTOs are doubled.
partial class NativeBundleHarness
{
    private static string Text(string text) => text;
    private static Dictionary<string, string> Attributes(params string[] pairs) => Enumerable.Range(0, pairs.Length / 2)
        .ToDictionary(i => pairs[i * 2], i => pairs[i * 2 + 1]);
    internal static class PegasusUtil { internal enum ProductType { PRODUCT_TYPE_BOOSTER, PRODUCT_TYPE_CARD_BACK, PRODUCT_TYPE_HERO, PRODUCT_TYPE_SELLABLE_DECK, PRODUCT_TYPE_DRAFT, PRODUCT_TYPE_PET, PRODUCT_TYPE_COSMETIC_COIN } }
    internal static class Network
    {
        internal sealed class Bundle
        {
            internal long? PMTProductID;
            internal string DisplayName, DisplayDescription, VirtualCurrencyCode;
            internal string ProductEvent = "unexpected event";
            internal Dictionary<string, string> Attributes;
            internal CostInfo? GoldCost;
            internal List<BundleItem> Items;
            internal struct CostInfo { internal int CurrentCost, OriginalCost; }
        }
        internal sealed class BundleItem { internal PegasusUtil.ProductType ItemType; internal int ProductData, Quantity, BaseQuantity; }
        internal sealed class ShopSection
        {
            internal string InternalName, Label;
            internal uint SortOrder;
            internal Dictionary<string, string> Attributes;
            internal List<Tuple<long, uint>> ProductOrder;
        }
    }
    internal static void Run(Action<bool, string> check)
    {
        var englishSections = BuildSections();
        try
        {
            global::GameStrings.Translations["GLUE_STORE_MODE_CARDS"] = "[xx] Cards";
            global::GameStrings.Translations["GLOBAL_CLASS_DRUID"] = "[xx] Druid";
            var localized = BuildSections();
            check(localized.Select(s => s.InternalName).SequenceEqual(englishSections.Select(s => s.InternalName)), "localization preserves shop section ids and order");
            check(localized.Single(s => s.InternalName == NativeShopTabs.Cards).Label == "[xx] Cards"
                && localized.Single(s => s.InternalName == "deck_druid").Label == "[xx] Druid", "shop and class labels localize at catalogue build time");
        }
        finally { global::GameStrings.Translations.Clear(); }
        foreach (var product in NativeShopPurchase.Products)
        {
            var bundle = BuildBundle(product, 123);
            check(bundle.PMTProductID == NativeShopPurchase.PmtId(product) && bundle.GoldCost.Value.CurrentCost == 123
                && bundle.GoldCost.Value.OriginalCost == 123 && bundle.VirtualCurrencyCode == "XSH", "native bundle uses supplied profile quote");
            var expected = product.Kind switch {
                "Pack" => PegasusUtil.ProductType.PRODUCT_TYPE_BOOSTER,
                "Coin" => PegasusUtil.ProductType.PRODUCT_TYPE_COSMETIC_COIN,
                "CardBack" => PegasusUtil.ProductType.PRODUCT_TYPE_CARD_BACK,
                "HeroSkin" => PegasusUtil.ProductType.PRODUCT_TYPE_HERO,
                "Pet" when product.Pet != null => PegasusUtil.ProductType.PRODUCT_TYPE_PET,
                "HubTheme" or "Board" or "Pet" => PegasusUtil.ProductType.PRODUCT_TYPE_DRAFT,
                _ => PegasusUtil.ProductType.PRODUCT_TYPE_SELLABLE_DECK };
            check(bundle.Items.Count == 1 && bundle.Items[0].ItemType == expected
                && bundle.Items[0].Quantity == 1 && bundle.Items[0].BaseQuantity == 1
                && bundle.Items[0].ProductData == NativeShopPurchase.NativeItemId(product), "native item " + product.Id);
            check(bundle.ProductEvent == null && !bundle.Attributes["tags"].Contains("preorder")
                && !bundle.Attributes["tags"].Contains("hide_owned"), "no retail timing or hidden ownership");
            if (product.Kind == "Recipe" || product.Kind == "Deck") {
                check(bundle.Attributes["tags"].Split(',').Contains("standalone_variant")
                    && bundle.Attributes["tags"].Split(',').Contains("show_price")
                    && !bundle.Attributes["tags"].Split(',').Contains("on_sale"), "deck and recipe tiles show their quote without sale styling");
                check(bundle.DisplayDescription.Contains(product.Kind == "Recipe" ? "Recipe only — cards not included; build it in the deck builder" : "Recipe plus missing")
                    && bundle.DisplayDescription.Contains("123 gold"), "deck grant and quote explained");
                check(bundle.DisplayName == (product.Kind == "Recipe" ? LocalShop.Default.Recipe(product.RecipeId).Name + " (Recipe)" : product.Description),
                    product.Id + ": native title marks recipes");
            }
        }
        var kitDeck = NativeShopPurchase.Products.First(p => p.Kind == "Deck");
        var kit = BuildBundle(kitDeck, 60);
        check(kit.GoldCost.Value.CurrentCost == 60 && kit.GoldCost.Value.OriginalCost == 60
            && !kit.Attributes["tags"].Split(',').Contains("on_sale"), "kit deck bundle uses only the after-kit quote");
        RunPageVariants(check);
        check(NativeShopLayout.DeckRowStyle == "standardsingle", "deck rows default to standardsingle (live 2026-10-29)");
        try {
            foreach (string style in new[] { "standard", "standardsingle" }) {
                NativeShopLayout.DeckRowStyle = style;
                CheckSections(check, style);
            }
        }
        finally { NativeShopLayout.DeckRowStyle = NativeShopLayout.DefaultDeckRowStyle; }
    }

    internal static void RunDeckSectionFilters(Action<bool, string> check)
    {
        var original = NativeShopPurchase.Products.Select(p => p.Id).ToHashSet();
        var library = LocalShop.Default.Recipes;
        try {
            foreach (string style in new[] { "standard", "standardsingle" }) {
                NativeShopLayout.DeckRowStyle = style;
                // Mix a Deck-only offer and a lone Recipe among Rogue's remaining Deck/Recipe offerings.
                var meta = NativeShopDecks.ShopOrder(library.Where(r => r.Hero == "HERO_03" && r.Tag == "meta")).ToArray();
                string missing = meta[0].Id, loneRecipe = meta[1].Id;
                NativeShopPurchase.Seed(p => original.Contains(p.Id) && (p.Kind == "Deck" || p.Kind == "Recipe")
                    && library.Single(r => r.Id == p.RecipeId).Hero == "HERO_03" && !(p.Kind == "Recipe" && p.RecipeId == missing)
                    && !(p.Kind == "Deck" && p.RecipeId == loneRecipe), null);
                var sections = BuildSections();
                check(sections.Where(s => s.Attributes.ContainsKey("tab_id")).Select(s => s.InternalName).SequenceEqual(new[] { "decks_2" })
                    && sections.Where(s => s.Attributes.ContainsKey("subtab_id")).Select(s => s.InternalName).SequenceEqual(new[] { "deck_rogue" }),
                    "empty categories and class sub-tabs omitted");
                check(sections.Where(s => s.Attributes.ContainsKey("tab_id") || s.Attributes.ContainsKey("subtab_id")).All(s => s.Attributes["tab_order"] == "0"),
                    "omitted categories and classes leave contiguous order");
                check(sections.SelectMany(s => s.ProductOrder).Select(p => p.Item1).OrderBy(id => id)
                    .SequenceEqual(NativeShopPurchase.Products.Where(p => p.Kind == "Deck" || p.RecipeId == loneRecipe)
                        .Select(NativeShopPurchase.PmtId).OrderBy(id => id)), "partial offerings show each Deck and the lone Recipe exactly once");
                RunPageVariants(check);
                foreach (var section in sections.Where(s => s.ProductOrder.Count > 0)) {
                    check(section.ProductOrder.Count <= NativeShopLayout.Capacity(style) && section.Attributes["style"] == style, "partial deck sections fit chosen style");
                    check(section.ProductOrder.Select(p => NativeShopPurchase.Product(p.Item1))
                        .All(p => p.Kind == "Deck" || p.Kind == "Recipe" && p.RecipeId == loneRecipe),
                        "paired Recipes stay on the page while lone Recipes keep a tile");
                }
                NativeShopPurchase.Seed(p => original.Contains(p.Id) && p.Kind == "Recipe" && p.RecipeId == missing, null);
                sections = BuildSections();
                check(sections.Count == 3 && sections[0].InternalName == "decks_2" && sections[1].InternalName == "deck_rogue"
                    && sections[2].ProductOrder.Count == 1 && NativeShopPurchase.Product(sections[2].ProductOrder[0].Item1).Kind == "Recipe",
                    "recipe-only class still offered");
                RunPageVariants(check);
                NativeShopPurchase.Seed(_ => false, null);
                check(BuildSections().Count == 0, "empty catalogue omits every category");
            }
        }
        finally {
            NativeShopLayout.DeckRowStyle = NativeShopLayout.DefaultDeckRowStyle;
            NativeShopPurchase.Seed(p => original.Contains(p.Id), null);
        }
    }

    private static void CheckSections(Action<bool, string> check, string deckStyle)
    {
        var sections = BuildSections();
        var offered = NativeShopPurchase.Products;
        var library = LocalShop.Default.Recipes;
        bool any(string kind) => offered.Any(p => p.Kind == kind);
        // One Deck tile, or its Recipe when the Deck is not offered.
        LocalShopProduct[] Tiles(string recipeId) {
            var deck = offered.Where(p => p.Kind == "Deck" && p.RecipeId == recipeId).ToArray();
            return deck.Length > 0 ? deck : offered.Where(p => p.Kind == "Recipe" && p.RecipeId == recipeId).ToArray();
        }
        bool PageVariant(LocalShopProduct p) => p.Kind == "Recipe" && !Tiles(p.RecipeId).Contains(p);
        check(offered.All(p => NativeShopDecks.PageVariant(p) == PageVariant(p)),
            "page variants are exactly the offered Recipes whose Deck is offered, for every class");
        var tabs = sections.Where(s => s.Attributes.ContainsKey("tab_id")).ToArray();
        check(tabs.Select(s => s.InternalName).SequenceEqual(new[] { "cards", "decks_1", "decks_2", "skins", "themes" })
            && tabs.Select(s => s.Label).SequenceEqual(new[] { "Cards", "Decks I", "Decks II", "Skins", "Themes" }),
            "five owner categories in order");
        check(tabs.Select(s => int.Parse(s.Attributes["tab_order"])).SequenceEqual(Enumerable.Range(0, 5)), "category order attributes");
        check(tabs.Select(s => s.Attributes["tab_icon"]).SequenceEqual(new[] { "hs", "bg", "rs", "featured", "other" })
            && tabs.Select(s => s.Attributes["tab_icon"]).Distinct().Count() == tabs.Length, "five distinct framed atlas category icons");
        var layout = new Dictionary<string, (string Id, string Label, string Kind, string Hero)[]> {
            ["cards"] = new[] { ("packs", "Packs", "Pack", "") },
            ["skins"] = new[] { ("hero_skins", "Hero Skins", "HeroSkin", ""), ("card_backs", "Card Backs", "CardBack", ""), ("coin_list", "Coins", "Coin", ""), ("pet_list", "Pets", "Pet", "") },
            ["themes"] = new[] { ("board_list", "Boards", "Board", ""), ("theme_list", "Game Themes", "HubTheme", "") } };
        foreach (string category in new[] { NativeShopTabs.Decks1, NativeShopTabs.Decks2 })
            layout[category] = NativeShopTabs.DeckClasses.Where(c => c.Tab == category).Select(c => (c.Id, c.Label, "Deck", c.Hero)).ToArray();
        foreach (var top in tabs) {
            int start = sections.IndexOf(top);
            var inTab = sections.Skip(start + 1).TakeWhile(s => !s.Attributes.ContainsKey("tab_id")).ToArray();
            var subs = inTab.Where(s => s.Attributes.ContainsKey("subtab_id")).ToArray();
            var want = layout[top.InternalName].Where(t => t.Hero == "" ? any(t.Kind)
                : offered.Any(p => (p.Kind == "Deck" || p.Kind == "Recipe") && library.Any(r => r.Id == p.RecipeId && r.Hero == t.Hero))).ToArray();
            check(subs.Select(s => s.InternalName).SequenceEqual(want.Select(t => t.Id)) && subs.Select(s => s.Label).SequenceEqual(want.Select(t => t.Label)),
                top.InternalName + ": sub-tabs, labels and order (empty ones omitted)");
            check(subs.Select(s => int.Parse(s.Attributes["tab_order"])).SequenceEqual(Enumerable.Range(0, subs.Length)), top.InternalName + ": sub-tab order attributes");
            check(subs.Length <= 6, top.InternalName + ": fits the native six sub-tab slots");
            for (int i = 0; i < subs.Length; i++) {
                var parts = inTab.SkipWhile(s => s != subs[i]).Skip(1).TakeWhile(s => !s.Attributes.ContainsKey("subtab_id")).ToArray();
                var rows = parts.SelectMany(s => s.ProductOrder).Select(p => NativeShopPurchase.Product(p.Item1)).ToArray();
                check(rows.Length > 0, subs[i].InternalName + ": no empty sub-tabs");
                if (want[i].Hero == "") {
                    check(rows.All(p => p.Kind == want[i].Kind) && rows.Length == offered.Count(p => p.Kind == want[i].Kind),
                        subs[i].InternalName + ": every offer of its kind");
                    continue;
                }
                string hero = want[i].Hero;
                check(rows.All(p => (p.Kind == "Deck" || p.Kind == "Recipe") && library.Single(r => r.Id == p.RecipeId).Hero == hero),
                    subs[i].InternalName + ": only its class's decks and recipes");
                // Continuation chunks read "Meta (cont.)" / "Fan favourite (cont.)"; the first chunk of a group has the plain label.
                string Group(string label) => label.EndsWith(" (cont.)", StringComparison.Ordinal) ? label.Substring(0, label.Length - 8) : label;
                var tags = parts.Select(s => Group(s.Label)).ToArray();
                check(parts.Where((s, n) => n == 0 || Group(parts[n - 1].Label) != Group(s.Label)).All(s => !s.Label.EndsWith(" (cont.)"))
                    && parts.Where((s, n) => n > 0 && Group(parts[n - 1].Label) == Group(s.Label)).All(s => s.Label == Group(s.Label) + " (cont.)"),
                    subs[i].InternalName + ": continuation headers say (cont.)");
                check(tags.All(t => t == "Meta" || t == "Fan favourite") && tags.SkipWhile(t => t == "Meta").All(t => t == "Fan favourite"),
                    subs[i].InternalName + ": Meta sections before Fan favourite sections");
                foreach (var group in new[] { ("meta", "Meta"), ("fan", "Fan favourite") }) {
                    var chunks = parts.Where(s => Group(s.Label) == group.Item2).ToArray();
                    string id = "local-decks-" + hero.ToLowerInvariant() + "-" + group.Item1;
                    check(chunks.Select(s => s.InternalName).SequenceEqual(chunks.Select((s, n) => id + (n == 0 ? "" : "-" + n))), "deck group section ids");
                    // Every deck section carries the native divider style, including the first (handled by the iterator patch).
                    check(chunks.All(s => s.Attributes.TryGetValue("divider_options", out var d) && d == NativeShopLayout.DeckHeaders),
                        "deck group header divider on every deck section");
                    var expected = NativeShopDecks.ShopOrder(library.Where(r => r.Hero == hero && r.Tag == group.Item1))
                        .SelectMany(r => Tiles(r.Id)).ToArray();
                    check(chunks.SelectMany(s => s.ProductOrder).Select(p => NativeShopPurchase.Product(p.Item1).Id).SequenceEqual(expected.Select(p => p.Id)),
                        subs[i].InternalName + ": " + group.Item2 + " ordered Deck tiles or lone Recipes");
                    check(chunks.Select(s => s.ProductOrder.Count).SequenceEqual(NativeShopLayout.Chunks(expected.Length, deckStyle)),
                        "deck sections fill each style's capacity before continuing");
                }
            }
        }
        foreach (var top in new[] { ("board_list", "Board"), ("theme_list", "HubTheme") }) {
            var tiles = sections.SkipWhile(s => s.InternalName != top.Item1).Skip(1).TakeWhile(s => !s.Attributes.ContainsKey("subtab_id") && !s.Attributes.ContainsKey("tab_id"))
                .SelectMany(s => s.ProductOrder).Select(p => NativeShopPurchase.Product(p.Item1)).ToArray();
            check(tiles.Length > 0 && (top.Item2 == "HubTheme" ? tiles[0].HubTheme == LocalHubCosmetics.RandomTheme
                && tiles[1].HubTheme == LocalHubCosmetics.ClassicTheme : tiles[0].Board == LocalHubCosmetics.RandomBoard && tiles[1].Board == 1),
                top.Item1 + ": Random owned leads, then the free default");
            check(tiles.SkipWhile(p => p.Price == 0).All(p => p.Price > 0), top.Item1 + ": free looks before the ones for sale");
        }
        check(sections.SelectMany(s => s.ProductOrder).Select(p => p.Item1).OrderBy(id => id)
            .SequenceEqual(offered.Where(p => !PageVariant(p)).Select(NativeShopPurchase.PmtId).OrderBy(id => id)),
            "every offered Deck, lone Recipe and other product exactly once; paired Recipes only on pages");
        check(sections.All(s => s.SortOrder == sections.IndexOf(s)), "monotonic section order");
        foreach (var section in sections.Where(s => s.ProductOrder.Count != 0)) {
            check(section.Attributes["tags"].Contains("show_if_all_owned") && !section.Attributes["tags"].Contains("hide_owned"), "owned section remains visible");
            string style = section.Attributes.TryGetValue("style", out var value) ? value : "default";
            int repeats = section.Attributes.TryGetValue("max_layout_count", out var count) ? int.Parse(count) : style == "default" ? 1 : 3;
            check(section.ProductOrder.Count <= (style == "standardsingle" ? 4 : 2) * repeats, "section stays within native layout capacity: " + section.InternalName);
            if (section.InternalName != "local-packs")
                check(section.Attributes["style"] == (section.InternalName.StartsWith("local-decks-") ? deckStyle : "standardsingle"), "native section geometry");
        }
        if (any("HeroSkin"))
            check(sections.Where(s => s.InternalName.StartsWith("local-skins-")).Select(s => s.Label)
                .SequenceEqual(NativeShopTabs.DeckClasses.Select(c => c.Label)), "hero class order");
        check(sections.Where(s => s.InternalName.StartsWith("local-card-backs")).SelectMany(s => s.ProductOrder).Select(p => p.Item1)
            .SequenceEqual(offered.Where(p => p.Kind == "CardBack").Select(NativeShopPurchase.PmtId)), "card back chunks preserve catalogue order");
    }
}
