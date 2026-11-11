// Shared production post-commit projection; only Unity managers and notifications are doubled.
partial class NativeShopProjectionHarness
{
    private static LocalProfile Profile;
    private static Dictionary<long, LocalDeck> decks = new();
    private static int grants, boosters, cosmetics, ranks;
    private static class NativeTestControl { internal static void AssertMainThread() { } }
    private static void PublishBoosters() => boosters++;
    private static void RefreshCosmetics() => cosmetics++;
    private static void AddNativeGrants(Dictionary<string, int> cards) {
        foreach (var pair in cards) { CollectionManager.Instance.Counts[pair.Key] = CollectionManager.Instance.GetTotalOwnedCount(pair.Key) + pair.Value; grants += pair.Value; }
    }
    private enum TAG_PREMIUM { NORMAL, GOLDEN }
    private sealed class NativeDeck { internal bool Loaded; internal void MarkNetworkContentsLoaded() => Loaded = true; }
    private static class NetCache { internal sealed class DeckHeader { internal long Id; } }
    private static NetCache.DeckHeader ExpectedHeader(long id) => new() { Id = id };
    private static void ProjectMissingDraftCards(NativeDeck deck, LocalCustomDeck saved) { }
    private static void ProjectSideboards(NativeDeck deck, List<LocalSideboard> boards) { }
    private static void ValidateNative(NativeDeck deck) {
        if (CollectionManager.Instance.FailValidation) { CollectionManager.Instance.FailValidation = false; throw new InvalidOperationException("projection failed after creation"); }
    }
    private static void SynchronizeExpectedNativeRanks() => ranks++;
    private static System.Reflection.MethodInfo Required(Type type, string method, Type[] types) => type.GetMethod(method, types);
    private sealed class CollectionManager
    {
        internal static CollectionManager Instance;
        internal Dictionary<string, int> Counts;
        internal bool FailCreation, FailValidation;
        internal int Creates;
        private readonly Dictionary<long, NativeDeck> saved = new();
        internal static CollectionManager Get() => Instance;
        internal int GetTotalOwnedCount(string id) => Counts.TryGetValue(id, out int count) ? count : 0;
        internal int GetOwnedCount(string id, TAG_PREMIUM premium) => premium == TAG_PREMIUM.NORMAL ? GetTotalOwnedCount(id) : 0;
        internal NativeDeck GetDeck(long id) => saved.TryGetValue(id, out var deck) ? deck : null;
        internal NativeDeck GetBaseDeck(long id) => GetDeck(id);
        public void OnDeckCreated(NetCache.DeckHeader header, int? value) {
            if (FailCreation) { FailCreation = false; throw new InvalidOperationException("projection failed before creation"); }
            Creates++; saved.Add(header.Id, new NativeDeck());
        }
    }
    internal static void Run(Action<bool, string> check, Func<string, int, string> fresh)
    {
        foreach (bool beforeCreation in new[] { true, false }) {
            using var profile = new LocalProfile(fresh("native-projection-" + beforeCreation, 100000));
            Profile = profile; decks = new(); grants = boosters = cosmetics = ranks = 0;
            CollectionManager.Instance = new CollectionManager { Counts = profile.Collection.ToDictionary(x => x.Key, x => x.Value),
                FailCreation = beforeCreation, FailValidation = !beforeCreation };
            var product = NativeShopPurchase.Products.First(p => p.Kind == "Deck");
            var receipt = new NativeShopPurchase().Commit(profile, 500, NativeShopPurchase.PmtId(product), "XSH", 1, profile.PurchasePrice(product.Id));
            int gold = profile.Gold;
            bool failed = false;
            try { RefreshShopPurchase(receipt); } catch (Exception) { failed = true; }
            check(failed && decks.Count == 1, "partial projection failure after durable receipt");
            RefreshShopPurchase(receipt); RefreshShopPurchase(receipt);
            check(profile.Gold == gold && profile.PurchaseSequence == 1, "projection retry never recharges");
            check(grants == receipt.Granted.Cards.Sum(c => c.CountAfter - c.CountBefore), "retry never duplicates card grants");
            // Decision 2026-10-25: bought locked Core copies project like any other grant, so the native deck is complete.
            check(receipt.Granted.Cards.Any(c => LocalCardCatalog.IsCore(c.CardId))
                && receipt.Granted.Cards.All(c => CollectionManager.Instance.GetTotalOwnedCount(c.CardId) == profile.Collection[c.CardId]),
                "locked Core grants reach the native collection");
            check(profile.GetDraft(receipt.Granted.DeckId.Value).Cards.GroupBy(id => id)
                .All(g => CollectionManager.Instance.GetTotalOwnedCount(g.Key) >= g.Count()), "native purchased deck has every card owned");
            check(CollectionManager.Instance.Creates == 1 && CollectionManager.Instance.GetDeck(receipt.Granted.DeckId.Value).Loaded,
                "retry finishes saved deck once, beforeCreation=" + beforeCreation);
            check(cosmetics == 2 && ranks == 2 && boosters == 3, "shared projection completes notifications after retry");
        }
        Profile = null;
    }
}
