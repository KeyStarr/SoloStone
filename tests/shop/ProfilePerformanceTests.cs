using System.Diagnostics;
using System.Reflection;
using System.Xml.Serialization;
using SabberStoneCore.Model;

internal static class ProfilePerformanceTests
{
    internal static void Run(string root)
    {
        string path = Path.Combine(root, "performance.xml");
        using (var fresh = new LocalProfile(path)) fresh.SelectCardBack(0);
        var serializer = new XmlSerializer(typeof(LocalProfileData));
        LocalProfileData data;
        using (var file = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(file)!;
        data.GoldBalance = 100000;
        WelcomeGiftTests.OwnGrantOnly(data);
        using (var file = File.Create(path)) serializer.Serialize(file, data);
        using (var buyer = new LocalProfile(path)) {
            buyer.Purchase("deck-manastorm-mage", 1, buyer.PurchasePrice("deck-manastorm-mage"), "perf-recipe");
            for (int i = 0; i < 150; i++) {
                var match = buyer.Begin("mage", "priest", i);
                buyer.Settle(match.Id, "Tied", 10, false);
                buyer.AcknowledgeRewardPresentation(match.Id);
            }
        }
        using (var file = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(file)!;
        // One durable deck past the visible limit exercises a full library (MaximumCustomDecks visible) and the hidden extra.
        data.CustomDecks[0].SortOrder = 0;
        var recipe = LocalShop.Default.Recipe("manastorm-mage");
        var recipeCards = recipe.Cards.SelectMany(row => Enumerable.Repeat(row.Id, row.Count)).ToList();
        for (int i = 1; i <= LocalProfile.MaximumCustomDecks; i++) data.CustomDecks.Add(new LocalCustomDeck {
            Id = data.NextCustomDeckId++, SortOrder = i, Name = "Performance " + i, Hero = "HERO_08",
            Cards = i % 3 == 0 ? recipeCards.ToList() : LocalDecks.Get("mage").CardIds.Take(i % 3 == 1 ? 30 : 12).ToList()
        });
        data.Collection = Cards.All.Where(card => !LocalCardCatalog.IsCore(card.Id) && LocalProfile.KnownLedgerCard(card.Id))
            .GroupBy(card => card.Id).Select(group => group.First()).Select(card => new LocalCardCount {
                Id = card.Id, Count = LocalEconomy.PlayableCap(card), Acquired = LocalEconomy.PlayableCap(card)
            }).ToList();
        using (var file = File.Create(path)) serializer.Serialize(file, data);
        var timings = new List<(string Name, double Milliseconds, double Budget)>();
        var timer = Stopwatch.StartNew();
        using var profile = new LocalProfile(path); // Includes XML parsing and every production validator.
        timings.Add(("load+validate", timer.Elapsed.TotalMilliseconds, 1500));
        if (profile.History.Count != 150 || profile.CustomDecks.Count != LocalProfile.MaximumCustomDecks || profile.GetDraft(data.CustomDecks[LocalProfile.MaximumCustomDecks].Id) == null
            || profile.Collection.Count < 1000 || profile.GetDraft(data.CustomDecks[0].Id).SourceRecipeId != "manastorm-mage")
            throw new Exception("Performance fixture must retain realistic history, decks and collection");
        // Generous for shared-machine shop reads; crafting keeps its tighter budgets.
        void Measure(string name, Action read, double budget = 500) {
            for (int i = 0; i < 3; i++) read();
            // Prior fixture allocations must not charge their collection to a hot-path read.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            timer.Restart();
            for (int i = 0; i < 20; i++) read();
            timings.Add((name + " x20", timer.Elapsed.TotalMilliseconds, budget));
        }
        Measure("CustomDecks", () => { if (profile.CustomDecks.Count != LocalProfile.MaximumCustomDecks) throw new Exception("Visible decks changed"); });
        Measure("CustomDeck", () => { if (profile.CustomDeck != null) throw new Exception("Multiple decks cannot expose legacy singleton"); });
        Measure("GetDraft(id)", () => { if (profile.GetDraft(data.CustomDecks[0].Id).Cards.Count != 30) throw new Exception("Recipe deck changed"); });
        Measure("Collection", () => GC.KeepAlive(profile.Collection));
        Measure("Gold", () => GC.KeepAlive(profile.Gold));
        // Native shop open: RefreshPrices then UpdateProductStatus quote every offer (two passes). Reported, not asserted.
        var offered = LocalShop.Default.Products.Where(p => !p.verify && !LocalShop.Default.Withheld(p)).Select(p => p.Id).ToArray();
        void QuoteOpen() { foreach (var id in offered) GC.KeepAlive(profile.PurchasePrice(id)); foreach (var id in offered) GC.KeepAlive(profile.PurchasePrice(id)); }
        QuoteOpen(); // Warm lazies and JIT.
        profile.SelectCardBack(0); // A commit: the next open sees a changed profile.
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        timer.Restart(); QuoteOpen();
        Console.WriteLine($"PERF shop open quotes after a profile change ({offered.Length * 2} PurchasePrice): {timer.Elapsed.TotalMilliseconds:F2} ms");
        timer.Restart(); QuoteOpen();
        Console.WriteLine($"PERF shop open quotes, unchanged profile ({offered.Length * 2} PurchasePrice): {timer.Elapsed.TotalMilliseconds:F2} ms");
        Measure("Stars", () => { if (profile.Stars != 0) throw new Exception("Ties changed stars"); });
        Measure("status inputs", () => {
            GC.KeepAlive(profile.CustomDeck); GC.KeepAlive(profile.LastPack); GC.KeepAlive(profile.Rank);
            GC.KeepAlive(profile.Stars); GC.KeepAlive(profile.Gold); GC.KeepAlive(profile.History.Count);
            GC.KeepAlive(profile.Pending); GC.KeepAlive(profile.UnopenedPacks); GC.KeepAlive(profile.Collection.Count);
            GC.KeepAlive(profile.Collection.Values.Sum()); GC.KeepAlive(profile.CustomDecks.Count);
        });
        var crafting = new LocalCraftingLedger(profile);
        var collectibleIds = Cards.All.Where(card => card.Collectible).Select(card => card.Id).ToArray();
        var changedIds = collectibleIds.Where(id => LocalCrafting.TryGetPrice(id, out _, out _)).Take(50).ToArray();
        if (changedIds.Length != 50) throw new Exception("Crafting fixture needs fifty craftable cards");
        Measure("crafting mass extras over all collectible ids", () => {
            var owned = crafting.OwnedCounts();
            long amount = 0;
            foreach (string id in collectibleIds)
                if (LocalCrafting.TryGetPrice(id, out _, out int sell))
                    amount = checked(amount + (long)Math.Max(0, (owned.TryGetValue(id, out int count) ? count : 0)
                        - LocalCrafting.Playset(id)) * sell);
            if (amount != 0) throw new Exception("Playsets must have no extras");
        }, 200);
        Measure("crafting synchronize fifty cards", () => {
            var owned = crafting.OwnedCounts();
            foreach (string id in changedIds)
                if (!owned.TryGetValue(id, out int count) || count != LocalCrafting.Playset(id))
                    throw new Exception("Synchronize must retain authoritative playsets");
        }, 50);
        // Retain the old read algorithm only as a measured reference, never as the production budget.
        var ledger = ((Lazy<Card[]>)typeof(LocalProfile).GetField("ledgerCards", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).Value;
        long[] LinearDraftIds() => data.CustomDecks.Where(deck => LocalDecks.PlayerHeroes.Contains(deck.Hero)
            && deck.Cards.All(id => ledger.Concat(LocalDecks.CustomCards).Any(card => card.Id == id))
            && deck.Sideboards.SelectMany(row => row.Cards).All(id => LocalCardCatalog.InActiveMeta(Cards.FromId(id))))
            .OrderBy(deck => deck.SortOrder).ThenBy(deck => deck.Id).Take(LocalProfile.MaximumCustomDecks).Select(deck => deck.Id).ToArray();
        if (!LinearDraftIds().SequenceEqual(profile.CustomDecks.Select(deck => deck.Id))) throw new Exception("Indexed draft visibility changed");
        timer.Restart();
        for (int i = 0; i < 20; i++) GC.KeepAlive(LinearDraftIds());
        Console.WriteLine($"PERF reference linear CustomDecks x20: {timer.Elapsed.TotalMilliseconds:F2} ms (before-style reference)");
        // Isolate copy cost from persistence/validation; these synthetic receipts are never committed.
        var snapshotData = (LocalProfileData)typeof(LocalProfile).GetField("data", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(profile)!;
        snapshotData.Purchases = Enumerable.Range(0, 500).Select(i => new LocalPurchaseReceipt {
            Sequence = i + 1, RequestId = "snapshot-" + i, CatalogVersion = 1, ProductId = "pack-standard",
            Kind = "Pack", Quantity = 1, GoldPaid = 100,
            Granted = new LocalPurchaseGrants { Packs = new List<LocalPackCount> { new() { PackType = "standard", Count = 1 } } }
        }).ToList();
        foreach (var match in snapshotData.Matches) match.TierRewards = new List<LocalTierReward> {
            new() { MilestoneId = "snapshot-tier", FirstTime = true, OpponentNpcId = "priest", OpponentName = "Priest",
                Grants = new List<LocalTierGrant> { new() { Kind = LocalTierGrantKind.Pack, Ref = "standard", Count = 1 } } }
        };
        var snapshot = typeof(LocalProfile).GetMethod("Snapshot", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Measure("Snapshot (500 purchases, 150 tier-reward matches)", () => GC.KeepAlive(snapshot.Invoke(profile, null)));
        foreach (var timing in timings) Console.WriteLine($"PERF {timing.Name}: {timing.Milliseconds:F2} ms (budget {timing.Budget:F0} ms)");
        foreach (var timing in timings)
            if (timing.Milliseconds >= timing.Budget) throw new Exception($"Performance regression: {timing.Name} {timing.Milliseconds:F2} ms >= {timing.Budget} ms");
        // Returned snapshots must remain detached after catalogue caching.
        var detached = profile.CustomDecks[0];
        int storedCount = detached.Cards.Count;
        detached.Cards.Clear();
        if (profile.GetDraft(detached.Id).Cards.Count != storedCount) throw new Exception("Getter leaked mutable deck state");
    }
}
