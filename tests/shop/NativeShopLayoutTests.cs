// Live lane-1 2026-10-25: no native "Reserved" teaser tiles, no empty sub-tab buttons, no doubled punctuation, and
// stale tile presses expire. Production hooks run against section, slot, tab and input doubles.
partial class NativeBundleHarness
{
    private sealed class SectionData { internal string SectionId; }
    private sealed class ShopSection { internal SectionData ProductSectionData = new(); }
    // Transform tree double: activeSelf is per object, as in Unity.
    private sealed class Transform
    {
        internal Transform parent; internal readonly GameObject gameObject;
        internal Transform(Transform parent = null) { this.parent = parent; gameObject = new GameObject(this); }
    }
    private sealed class GameObject
    {
        internal readonly Transform transform; internal bool activeSelf = true;
        internal GameObject(Transform owner) { transform = owner; }
        internal static int Calls; // Every SetActive, so an idle per-frame sync can be proven to change nothing.
        internal void SetActive(bool on) { Calls++; activeSelf = on; }
    }
    private sealed class ShopSubTab { internal Transform transform; internal ShopSubTab(Transform transform) { this.transform = transform; } }
    private static readonly HashSet<GameObject> hiddenSubTabs = new();
    // Production per-frame sub-tab cache (SyncSubTabs builds it, SyncSubTabsFrame reuses it).
    private static object subTabController;
    private static System.Collections.IList subTabList;
    private static object[] subTabSlots;
    private static GameObject[] subTabRoots;

    internal static void RunLayout(Action<bool, string> check)
    {
        RunDeckDividers(check);
        RunSlots(check);
        RunSectionAttributes(check);
        RunText(check);
        RunSubTabs(check);
        RunPressExpiry(check);
    }

    // Pinned GetLayoutMapping row count and LoadSlotsCoroutine slot loop, with the production HideEmptySlot prefix.
    private static void RunSlots(Action<bool, string> check)
    {
        foreach (string style in new[] { "standardsingle", "standard", "" })
            foreach (int count in Enumerable.Range(1, 12).Concat(new[] { 37, 47, 5 }))
            {
                int perRow = NativeShopLayout.SlotsPerRow(style);
                var chunks = NativeShopLayout.Chunks(count, style).ToArray();
                check(chunks.Sum() == count && chunks.All(c => c > 0 && c <= NativeShopLayout.Capacity(style)), $"{style}/{count}: sections cover every product within native capacity");
                foreach (int chunk in chunks)
                {
                    int rows = NativeShopLayout.Rows(chunk, perRow, NativeShopLayout.MaxRows), empty = NativeShopLayout.EmptySlots(chunk, perRow, NativeShopLayout.MaxRows);
                    check(rows * perRow >= chunk && empty == rows * perRow - chunk && empty < perRow, $"{style}/{count}: only the last row is short, never a whole empty row");
                    var section = new ShopSection { ProductSectionData = { SectionId = "local-skins-hero_04" } };
                    int reserved = 0, shown = 0;
                    for (int i = 0; i < rows * perRow; i++)
                    {
                        var slot = new ShopSlot { IsEmpty = i >= chunk };
                        if (HideEmptySlot(section, slot)) { if (slot.IsEmpty) reserved++; } // Native SetReservedSlotDataModel would run.
                        if (slot.Enabled) shown++;
                    }
                    check(reserved == 0 && shown == chunk, $"{style}/{count}: {chunk} products shown, {empty} empty slots hidden, no Reserved tile");
                }
            }
        check(NativeShopLayout.Chunks(5, "standardsingle").SequenceEqual(new[] { 5 }) && NativeShopLayout.Rows(5, 4, 3) == 2
            && NativeShopLayout.EmptySlots(5, 4, 3) == 3, "5 hero skins: one section, 4 + 1 tiles, 3 hidden empty slots");
        check(NativeShopLayout.Chunks(47, "standardsingle").SequenceEqual(new[] { 12, 12, 12, 11 }) && NativeShopLayout.EmptySlots(11, 4, 3) == 1,
            "47 card backs: three full sections and one of 11 tiles, its 1 empty slot hidden");
        var retail = new ShopSection { ProductSectionData = { SectionId = "featured" } };
        var retailSlot = new ShopSlot { IsEmpty = true };
        check(HideEmptySlot(retail, retailSlot) && retailSlot.Enabled, "foreign sections keep native reserved slots");
        var local = new ShopSection { ProductSectionData = { SectionId = "local-card-backs-3" } };
        Isolated = false;
        var outside = new ShopSlot { IsEmpty = true };
        check(HideEmptySlot(local, outside) && outside.Enabled, "hook inert outside isolation");
        Isolated = true;
        var product = new ShopSlot { IsEmpty = false };
        check(HideEmptySlot(local, product) && product.Enabled, "product slot untouched");
        check(HideEmptySlot(null, new ShopSlot { IsEmpty = true }) && HideEmptySlot(new ShopSection { ProductSectionData = null }, new ShopSlot { IsEmpty = true }),
            "missing section data falls back to the native slot");
    }

    // Native Attribute lowercases names and values, and section style lookups use the lowercase spelling.
    private static void RunSectionAttributes(Action<bool, string> check)
    {
        var sections = BuildSections();
        check(sections.All(s => s.Attributes.All(a => a.Key == a.Key.ToLowerInvariant() && a.Value == a.Value.ToLowerInvariant())),
            "section attributes survive the native lowercase conversion unchanged");
        foreach (var section in sections.Where(s => s.ProductOrder.Count != 0))
        {
            string style = section.Attributes.TryGetValue("style", out var value) ? value : "";
            check(style == "" || style == "standard" || style == "standardsingle", "known native style " + section.InternalName);
            check(section.ProductOrder.Count <= NativeShopLayout.Capacity(style)
                && (!section.Attributes.TryGetValue("max_layout_count", out var max) || int.Parse(max) == NativeShopLayout.MaxRows),
                "section within its native row capacity " + section.InternalName);
        }
        // Chunked sections keep catalogue order and number their continuations.
        var backs = sections.Where(s => s.InternalName.StartsWith("local-card-backs", StringComparison.Ordinal)).ToArray();
        check(backs.Select(s => s.InternalName).SequenceEqual(backs.Select((s, i) => i == 0 ? "local-card-backs" : "local-card-backs-" + i)),
            "card back continuation sections numbered in order");
    }

    private static void RunText(Action<bool, string> check)
    {
        check(Sentence("Pirates!") == "Pirates!" && Sentence("Pirates") == "Pirates." && Sentence("Why?") == "Why?"
            && Sentence("Done.") == "Done." && Sentence("Wait…") == "Wait…" && Sentence("Trailing ") == "Trailing." && Sentence("") == "" && Sentence(null) == "",
            "names ending in punctuation get no second stop");
        var pirates = new LocalShopProduct { Id = "cardback-pirates", Kind = "CardBack", CardBack = 1, Description = "Pirates!" };
        check(Description(pirates, 500) == "Pirates! 500 gold.", "card back page text: " + Description(pirates, 500));
        var plain = new LocalShopProduct { Id = "skin-x", Kind = "HeroSkin", HeroSkin = "HERO_01", Description = "Magni Bronzebeard" };
        check(Description(plain, 1000) == "Magni Bronzebeard. 1000 gold.", "plain name keeps its stop");
        // Cosmetic price tiers: the offer carries each item's own tier price into the native bundle text.
        check(NativeShopPurchase.Products.Where(p => p.Kind == "HeroSkin" || p.Kind == "CardBack").Select(p => p.Price).Distinct().OrderBy(p => p)
            .SequenceEqual(new[] { 300, 500, 800, 1000, 1800, 2500 }), "native offer spans all six cosmetic tier prices");
        var ragnaros = NativeShopPurchase.Products.Single(p => p.Id == "heroskin-HERO_01bb");
        check(BuildBundle(ragnaros, ragnaros.Price).DisplayDescription.Contains("Ragnaros the Firelord. 2500 gold."), "legendary skin bundle shows its tier price");
        check(NativeShopPurchase.Products.All(p => !System.Text.RegularExpressions.Regex.IsMatch(Description(p, 100), @"[.!?…]\.")),
            "no catalogue description doubles its punctuation");
        var real = NativeShopPurchase.Products.First(p => p.Kind == "Recipe");
        var broken = new LocalShopProduct { Id = "recipe-broken", Kind = "Recipe", NativeId = real.NativeId, RecipeId = "missing-recipe", Description = "Broken recipe" };
        int refused = refusals.Count;
        var bundle = BuildBundle(broken, 50);
        check(bundle.DisplayName == "Broken recipe" && bundle.DisplayDescription.Contains("50 gold") && refusals.Count == refused + 1
            && refusals.Last().Contains("recipe-broken"), "missing recipe title falls back to the product description, logged, bundle still built");
        check(DisplayName(real) == LocalShop.Default.Recipe(real.RecipeId).Name + " (Recipe)", "recipe title unchanged when the recipe resolves");
    }

    // Prefab shape the live plates imply: bar -> slot container (plate + nested sub-tab widget -> template -> ShopSubTab).
    private static void RunSubTabs(Action<bool, string> check)
    {
        var shop = new Transform();
        var controllerRoot = new Transform(shop);
        var bar = new Transform(controllerRoot);
        var containers = Enumerable.Range(0, 6).Select(_ => new Transform(bar)).ToList();
        var plates = containers.Select(container => new Transform(container)).ToList();
        var tabs = containers.Select(container => new ShopSubTab(new Transform(new Transform(new Transform(container))))).ToList();
        var controller = new ShopTabController { ActiveTabIndex = 0, Used = 4, transform = controllerRoot };
        controller.SubTabs.AddRange(tabs);
        bool Drawn(int i) => Chain(plates[i]).All(node => node.gameObject.activeSelf); // activeInHierarchy
        bool[] Shown() => Enumerable.Range(0, 6).Select(Drawn).ToArray();
        void Rebind() { SyncSubTabs(controller, false); SyncSubTabs(controller, true); } // Prefix, native rebind, postfix.
        var roots = NativeShopLayout.ExclusiveRoots(tabs.Select(t => t.transform).ToList(), node => node.parent, controllerRoot);
        check(roots.SequenceEqual(containers), "each slot's root is its own bar container, plate included");
        Rebind();
        check(Shown().SequenceEqual(new[] { true, true, true, true, false, false }) && !containers[4].gameObject.activeSelf
            && tabs[4].transform.gameObject.activeSelf, "4 local sub-tabs shown; 2 unused containers hidden, plates with them");
        Rebind();
        check(Shown().SequenceEqual(new[] { true, true, true, true, false, false }) && hiddenSubTabs.Count == 2, "rebind is idempotent");
        // Live 2026-10-25 (lane 1): reveal waited forever on a shop sitting idle. The per-frame tick (TickSubTabs) must
        // be a no-op at idle: no SetActive, no catalogue dirtying, no refusal.
        int setActive = GameObject.Calls, dirty = ProductCatalog.Dirty, refusalCount = refusals.Count;
        for (int frame = 0; frame < 120; frame++) SyncSubTabs(controller, true);
        check(GameObject.Calls == setActive && ProductCatalog.Dirty == dirty && refusals.Count == refusalCount
            && Shown().SequenceEqual(new[] { true, true, true, true, false, false }), "idle per-frame sync changes nothing and dirties nothing");
        containers[4].gameObject.SetActive(true); // The bar's own state re-shows an unused plate.
        setActive = GameObject.Calls;
        SyncSubTabs(controller, true); SyncSubTabs(controller, true);
        check(!containers[4].gameObject.activeSelf && GameObject.Calls == setActive + 1, "a re-shown unused plate is hidden once, then idle again");
        controller.Used = 5;
        SyncSubTabs(controller, false);
        check(Drawn(4) && !Drawn(5), "slot reactivated before a rebind that shows it");
        SyncSubTabs(controller, true);
        check(Shown().SequenceEqual(new[] { true, true, true, true, true, false }), "fifth sub-tab shown again");
        containers[1].gameObject.SetActive(false); // The game's own state, not this hook's.
        Rebind();
        check(!containers[1].gameObject.activeSelf, "a slot the game deactivated is never reactivated");
        containers[1].gameObject.SetActive(true);
        controller.ActiveTabIndex = -1; controller.Used = 0;
        Rebind();
        check(Shown().SequenceEqual(new[] { true, true, true, true, true, false }), "no active tab: untouched, like the native early return");
        controller.ActiveTabIndex = 0; controller.Used = 4;
        Rebind();
        // A root hidden at an older (lower) level is still restored when its slot is used again.
        containers[5].gameObject.SetActive(true); hiddenSubTabs.Remove(containers[5].gameObject);
        tabs[5].transform.parent.gameObject.SetActive(false); hiddenSubTabs.Add(tabs[5].transform.parent.gameObject);
        controller.Used = 6; Rebind();
        check(Shown().All(a => a) && tabs[5].transform.parent.gameObject.activeSelf && hiddenSubTabs.Count == 0, "any hidden object on a used slot's chain is restored");
        controller.Used = 4; Rebind();
        Enabled = false;
        Rebind();
        check(Shown().All(a => a) && hiddenSubTabs.Count == 0, "disabled local shop restores every hidden slot");
        Enabled = true;
        Isolated = false; Rebind(); check(Shown().All(a => a), "inert outside isolation");
        Isolated = true;
        // Flat bar (plates are siblings of the slots): each root is the slot's own widget chain below the bar.
        var flat = new Transform(controllerRoot);
        var flatTabs = Enumerable.Range(0, 3).Select(_ => new ShopSubTab(new Transform(new Transform(flat)))).ToList();
        check(NativeShopLayout.ExclusiveRoots(flatTabs.Select(t => t.transform).ToList(), node => node.parent, controllerRoot)
            .SequenceEqual(flatTabs.Select(t => t.transform.parent)), "flat bar: roots stop under the shared bar");
        // Two slots in one container cannot be split: refused, bar untouched.
        var shared = new Transform(bar);
        var sharedController = new ShopTabController { ActiveTabIndex = 0, Used = 1, transform = controllerRoot };
        var pair = new[] { new ShopSubTab(shared), new ShopSubTab(new Transform(shared)) };
        sharedController.SubTabs.AddRange(pair);
        int refused = refusals.Count;
        SyncSubTabs(sharedController, false); SyncSubTabs(sharedController, true);
        check(shared.gameObject.activeSelf && pair[1].transform.gameObject.activeSelf && refusals.Count > refused
            && refusals.Skip(refused).All(r => r.Contains("share a root")), "nested slots refused, nothing hidden");
        // A slot whose climb reaches a scene root (no shared ancestor) is refused instead of hiding a whole hierarchy.
        var lone = new ShopTabController { ActiveTabIndex = 0, Used = 0, transform = new Transform() };
        var island = new Transform();
        lone.SubTabs.Add(new ShopSubTab(new Transform(island)));
        refused = refusals.Count;
        SyncSubTabs(lone, true);
        check(island.gameObject.activeSelf && refusals.Count > refused, "unanchored slot refused");
        // The controller's own chain is never a root.
        check(NativeShopLayout.ExclusiveRoots(new[] { controllerRoot }, node => node.parent, controllerRoot).Single() == null,
            "a slot on the controller chain gets no root");
        // A slot still loading: nothing computed this time; the next rebind or frame retries.
        controller.SubTabs.Add(null);
        Rebind();
        check(Shown().All(a => a), "loading slot: no partial roots, nothing hidden");
        controller.SubTabs.RemoveAt(controller.SubTabs.Count - 1);
        Rebind();
        check(Shown().SequenceEqual(new[] { true, true, true, true, false, false }), "loaded bar hides the unused slots again");
        // Per-frame tick (SyncSubTabsFrame): reuses the last full sync's roots while controller, list and slots are unchanged.
        var cachedRoots = subTabRoots;
        int frameCalls = GameObject.Calls, frameRefusals = refusals.Count;
        for (int frame = 0; frame < 120; frame++) SyncSubTabsFrame(controller);
        check(ReferenceEquals(subTabRoots, cachedRoots) && cachedRoots.SequenceEqual(containers.Select(c => c.gameObject))
            && GameObject.Calls == frameCalls && refusals.Count == frameRefusals
            && Shown().SequenceEqual(new[] { true, true, true, true, false, false }), "cached tick at idle: no recompute, no SetActive, no refusal");
        containers[5].gameObject.SetActive(true); // The bar's own state re-shows an unused plate.
        frameCalls = GameObject.Calls;
        SyncSubTabsFrame(controller); SyncSubTabsFrame(controller);
        check(!containers[5].gameObject.activeSelf && GameObject.Calls == frameCalls + 1 && ReferenceEquals(subTabRoots, cachedRoots),
            "cached tick re-hides a re-shown unused plate once");
        controller.Used = 5; SyncSubTabsFrame(controller);
        check(Shown().SequenceEqual(new[] { true, true, true, true, true, false }) && hiddenSubTabs.Count == 1, "cached tick restores a slot that became used");
        controller.Used = 4; SyncSubTabsFrame(controller);
        check(Shown().SequenceEqual(new[] { true, true, true, true, false, false }) && hiddenSubTabs.Count == 2, "and hides it again when unused");
        var replaced = controller.SubTabs[5];
        controller.SubTabs[5] = new ShopSubTab(new Transform(new Transform(new Transform(containers[5]))));
        SyncSubTabsFrame(controller);
        check(!ReferenceEquals(subTabRoots, cachedRoots) && subTabRoots.SequenceEqual(containers.Select(c => c.gameObject))
            && Shown().SequenceEqual(new[] { true, true, true, true, false, false }), "a replaced slot object recomputes through the full sync");
        controller.SubTabs[5] = replaced;
        controller.SubTabs.Add(null); SyncSubTabsFrame(controller);
        check(subTabRoots == null && Shown().SequenceEqual(new[] { true, true, true, true, false, false }), "a list that grew (still loading) drops the cache");
        controller.SubTabs.RemoveAt(controller.SubTabs.Count - 1); SyncSubTabsFrame(controller);
        check(subTabRoots != null, "loaded again: the next tick rebuilds the cache");
        Enabled = false; SyncSubTabsFrame(controller);
        check(Shown().All(a => a) && hiddenSubTabs.Count == 0, "disabled local shop: the cached tick restores every hidden slot");
        Enabled = true; SyncSubTabsFrame(controller);
        check(Shown().SequenceEqual(new[] { true, true, true, true, false, false }), "re-enabled: unused slots hidden again");
        var other = new ShopTabController { ActiveTabIndex = 0, Used = 4, transform = controllerRoot };
        other.SubTabs.AddRange(tabs);
        cachedRoots = subTabRoots; SyncSubTabsFrame(other);
        check(!ReferenceEquals(subTabRoots, cachedRoots) && ReferenceEquals(subTabController, other), "another controller recomputes");
        frameRefusals = refusals.Count;
        SyncSubTabs(sharedController, true); int refusedCalls = GameObject.Calls;
        for (int frame = 0; frame < 10; frame++) SyncSubTabsFrame(sharedController);
        check(GameObject.Calls == refusedCalls && shared.gameObject.activeSelf && subTabRoots != null && subTabRoots.Length == 0,
            "a refused layout is remembered: per-frame ticks leave it untouched");
        Isolated = false; frameCalls = GameObject.Calls; SyncSubTabsFrame(controller); check(GameObject.Calls == frameCalls, "cached tick inert outside isolation");
        Isolated = true;
    }
    private static IEnumerable<Transform> Chain(Transform node) { for (; node != null; node = node.parent) yield return node; }

    // Pinned PegUI double-click and custom-behaviour branches skip RELEASEALL; the press expires after its cycle.
    private static void RunPressExpiry(Action<bool, string> check)
    {
        var tile = new Clickable();
        InputCollection.Held = false;
        Time.frameCount = 1000; NoteTilePress(tile); ExpireTilePress();
        Time.frameCount++; ExpireTilePress();
        check(ReferenceEquals(pressClickable, tile), "semantic press survives to its release frame");
        Time.frameCount++; ExpireTilePress();
        check(pressClickable == null, "unreleased semantic press dropped after its cycle");
        InputCollection.Held = true; NoteTilePress(tile);
        for (int i = 0; i < 300; i++) { Time.frameCount++; ExpireTilePress(); }
        check(ReferenceEquals(pressClickable, tile), "held button keeps its press however long");
        InputCollection.Held = false; Time.frameCount++; ExpireTilePress();
        Time.frameCount++; ExpireTilePress();
        check(ReferenceEquals(pressClickable, tile), "mouse-up frame and the next keep the press for its release");
        Time.frameCount++; ExpireTilePress();
        check(pressClickable == null, "double-click or takeover mouse-up without RELEASEALL: press dropped");
        NoteTilePress(tile); ClearTilePress(new Clickable());
        check(ReferenceEquals(pressClickable, tile), "another tile's roll-out or disable keeps this press");
        ClearTilePress(tile);
        check(pressClickable == null, "roll-out or disable of the pressed tile ends its press");
        ExpireTilePress();
        check(pressClickable == null, "expiry with no press is a no-op");
    }
}
