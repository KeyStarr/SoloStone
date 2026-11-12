// Production cache builder, favorite prefix and notification path; only client boundaries are doubled.
internal static partial class NativeCoinsHarness
{
    private sealed class CoinTable {
        internal readonly HashSet<int> Ids = new() { 1, 2 };
        internal readonly HashSet<int> MissingCards = new();
        internal CoinRecord GetRecord(int id) => Ids.Contains(id) ? new CoinRecord { CardRecord = MissingCards.Contains(id) ? null : new CardRecord() } : null;
    }
    private sealed class CardRecord { internal string NoteMiniGuid = "GAME_005"; }
    private sealed class CoinRecord { internal CardRecord CardRecord; }
    private static class NativeMatch { internal static string NativeCoinCard(int id) => GameDbf.CosmeticCoin.GetRecord(id)?.CardRecord?.NoteMiniGuid; }
    private static class GameDbf { internal static readonly CoinTable CosmeticCoin = new(); }
    private sealed class NetCache {
        internal sealed class NetCacheCoins { internal HashSet<int> Coins = new(), FavoriteCoins = new(); }
        internal static readonly NetCache Instance = new();
        internal static NetCache Get() => Instance;
        internal readonly List<(int Id, bool Favorite)> Events = new();
        internal void ProcessNewFavoriteCoin(int id, bool favorite) {
            var snapshot = (NetCacheCoins)cache[typeof(NetCacheCoins)];
            if (favorite) snapshot.FavoriteCoins.Add(id); else snapshot.FavoriteCoins.Remove(id);
            Events.Add((id, favorite));
        }
    }
    private static LocalProfile Profile;
    private static readonly Dictionary<Type, object> cache = new();
    private static bool IsolatedLaunch = true, active = true, ready = true, failPublish, savedOnFailure;
    private static Exception failure;
    private static void CosmeticSelectionFailed(Exception error, bool saved) { failure = error; savedOnFailure = saved; }
    private static void Publish(object value) { if (failPublish) throw new IOException("publish fault"); cache[value.GetType()] = value; }
    internal static void Run(LocalProfile profile, Action<bool, string> check)
    {
        Profile = profile;
        try {
            var coins = CosmeticCoins();
            check(coins.Coins.SetEquals(new[] { 1, 2 }) && coins.FavoriteCoins.SetEquals(new[] { 1 }), "NetCacheCoins projects exact owned and favorite cosmetic IDs");
            RefreshCoins();
            check(!SelectLocalCoin(2, true) && Profile.FavoriteCoins.SequenceEqual(new[] { 1, 2 })
                && NetCache.Instance.Events.Contains((2, true)), "coin manager prefix skips retail and sends native favorite event");
            SelectLocalCoin(1, false); SelectLocalCoin(2, false);
            check(Profile.FavoriteCoins.SequenceEqual(new[] { 1 }) && ((NetCache.NetCacheCoins)cache[typeof(NetCache.NetCacheCoins)]).FavoriteCoins.SetEquals(new[] { 1 })
                && NetCache.Instance.Events.Contains((2, false)), "native coin last-favorite fallback is published and notified");
            savedOnFailure = true; failure = null;
            SelectLocalCoin(3, true);
            check(!savedOnFailure && failure is InvalidOperationException, "unknown native coin rejected before persistence");
            GameDbf.CosmeticCoin.Ids.Add(3); failure = null;
            SelectLocalCoin(3, true);
            check(!savedOnFailure && failure is InvalidOperationException, "unowned native coin rejected before persistence");
            ready = false; failure = null; SelectLocalCoin(2, true);
            check(failure is InvalidOperationException && !savedOnFailure, "native coin selection requires ready local profile");
            ready = true; failPublish = true; failure = null;
            SelectLocalCoin(2, true);
            check(savedOnFailure && failure is IOException && Profile.FavoriteCoins.Contains(2), "coin selection remains durable when native refresh fails");
            failPublish = false;
            GameDbf.CosmeticCoin.Ids.Remove(2);
            coins = CosmeticCoins();
            check(coins.Coins.SetEquals(new[] { 1 }) && coins.FavoriteCoins.SetEquals(new[] { 1 }), "unknown owned coin filtered from both cache sets before unsafe native random selection");
            check(Profile.OwnedCoins.Contains(2) && Profile.FavoriteCoins.Contains(2), "missing row preserves coin ledger");
            GameDbf.CosmeticCoin.Ids.Add(2);
            GameDbf.CosmeticCoin.MissingCards.Add(2);
            coins = CosmeticCoins();
            check(!coins.Coins.Contains(2) && !coins.FavoriteCoins.Contains(2), "coin with missing card record filtered from both cache sets");
            GameDbf.CosmeticCoin.MissingCards.Clear();
            RefreshCoins();
            Profile.SetCoinFavorite(2, false);
            RefreshCoins();
        }
        finally { Profile = null; ready = true; failPublish = false; cache.Clear(); }
    }
}
