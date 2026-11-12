using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;
using HarmonyLib;
using PegasusGame;

// Headless substitutes for the plugin host; production snapshots and hooks compile unchanged.
internal static partial class NativeUIProbe
{
    internal static bool ready = true;
    internal static bool IsolatedLaunch = true;
    internal static readonly string[] Heroes = LocalDecks.PlayerHeroes.ToArray();
    internal static readonly Dictionary<Type, object> cache = new Dictionary<Type, object>();
    private static void CosmeticSelectionFailed(Exception error, bool saved) { }
    private static void Publish<T>(T value) => cache[typeof(T)] = value;
    private static MethodInfo Required(Type type, string name) => AccessTools.Method(type, name)
        ?? throw new MissingMethodException(type.FullName, name);
    private static void Hook(Harmony harmony, Type type, string method, string prefix, Type[] parameters) =>
        harmony.Patch(AccessTools.Method(type, method, parameters), prefix: new HarmonyMethod(typeof(NativeUIProbe), prefix));
}

internal static class CosmeticsRuntimeTests
{
    private static readonly CollectionManager manager = (CollectionManager)RuntimeHelpers.GetUninitializedObject(typeof(CollectionManager));
    private static readonly CardBackManager backManager = (CardBackManager)RuntimeHelpers.GetUninitializedObject(typeof(CardBackManager));
    private static readonly NetCache netCache = (NetCache)RuntimeHelpers.GetUninitializedObject(typeof(NetCache));
    private static readonly Dictionary<string, int> owned = new Dictionary<string, int>();
    private static bool Manager(ref CollectionManager __result) { __result = manager; return false; }
    private static bool BackManager(ref CardBackManager __result) { __result = backManager; return false; }
    private static bool Cache(ref NetCache __result) { __result = netCache; return false; }
    private static readonly Dictionary<(string, TAG_PREMIUM), int> premiumOwned = new Dictionary<(string, TAG_PREMIUM), int>();
    private static bool Count(string __0, ref int __result) {
        __result = (owned.TryGetValue(__0, out int n) ? n : 0) + premiumOwned.Where(p => p.Key.Item1 == __0).Sum(p => p.Value); return false;
    }
    private static bool CountPremium(string __0, TAG_PREMIUM __1, ref int __result) {
        __result = __1 == TAG_PREMIUM.NORMAL ? (owned.TryGetValue(__0, out int n) ? n : 0)
            : (premiumOwned.TryGetValue((__0, __1), out int m) ? m : 0);
        return false;
    }
    private static bool Grant(List<CollectionManager.CardModification> __0) {
        foreach (var change in __0) {
            if (change.premium == TAG_PREMIUM.NORMAL) owned[change.cardID] = (owned.TryGetValue(change.cardID, out int n) ? n : 0) + change.count;
            else premiumOwned[(change.cardID, change.premium)] = (premiumOwned.TryGetValue((change.cardID, change.premium), out int m) ? m : 0) + change.count;
        }
        return false;
    }
    private static Network network;
    private static bool NetworkInstance(ref Network __result) { __result = network; return false; }
    private static bool MultipleFavorites(ref bool __result) { __result = true; return false; }
    private static bool BackSnapshot(ref NetCache.NetCacheCardBacks __result) {
        __result = (NetCache.NetCacheCardBacks)NativeUIProbe.cache[typeof(NetCache.NetCacheCardBacks)]; return false;
    }
    private static bool Favorite() => false;
    private static bool BackGrants(PegasusUtil.CardBackModifications __0) {
        var backs = (NetCache.NetCacheCardBacks)NativeUIProbe.cache[typeof(NetCache.NetCacheCardBacks)];
        foreach (var change in __0.CardBackModifications_) backs.CardBacks.Add(change.AssetCardBackId);
        return false;
    }
    private static bool FavoriteBack(int __0, bool __1) {
        var backs = (NetCache.NetCacheCardBacks)NativeUIProbe.cache[typeof(NetCache.NetCacheCardBacks)];
        if (__1) backs.FavoriteCardBacks.Add(__0); else backs.FavoriteCardBacks.Remove(__0);
        return false;
    }
    private static bool CoinRecord(int __0, ref CosmeticCoinDbfRecord __result) {
        __result = __0 == 1 ? (CosmeticCoinDbfRecord)RuntimeHelpers.GetUninitializedObject(typeof(CosmeticCoinDbfRecord)) : null;
        return false;
    }
    private static bool FavoriteCoin(int __0, bool __1) {
        var coins = (NetCache.NetCacheCoins)NativeUIProbe.cache[typeof(NetCache.NetCacheCoins)];
        if (__1) coins.FavoriteCoins.Add(__0); else coins.FavoriteCoins.Remove(__0);
        return false;
    }
    internal static void Run()
    {
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); }
        using var fixture = new CosmeticsFixture();
        var harmony = new Harmony("solostone.tests.cosmetics");
        void Patch(Type type, string method, string prefix, Type[] parameters = null) => harmony.Patch(
            AccessTools.Method(type, method, parameters), prefix: new HarmonyMethod(typeof(CosmeticsRuntimeTests), prefix));
        var oldCoins = GameDbf.CosmeticCoin;
        string path = Path.Combine(Path.GetTempPath(), "cosmetics-native-" + Guid.NewGuid() + ".xml");
        try {
            GameDbf.CosmeticCoin = (Dbf<CosmeticCoinDbfRecord>)RuntimeHelpers.GetUninitializedObject(typeof(Dbf<CosmeticCoinDbfRecord>));
            Patch(typeof(Dbf<CosmeticCoinDbfRecord>), "GetRecord", nameof(CoinRecord), new[] { typeof(int) });
            Patch(typeof(NetCache), "ProcessNewFavoriteCoin", nameof(FavoriteCoin));
            Patch(typeof(CollectionManager), "Get", nameof(Manager));
            Patch(typeof(NetCache), "Get", nameof(Cache));
            Patch(typeof(CardBackManager), "Get", nameof(BackManager));
            Patch(typeof(NetCache), "OnCardBackModifications", nameof(BackGrants));
            var sortedBack = new CardBackManager.OwnedCardBack { m_cardBackId = 122, m_owned = false };
            AccessTools.Field(typeof(CardBackManager), "m_sortedCardBacks").SetValue(backManager,
                new List<CardBackManager.OwnedCardBack> { sortedBack });
            Patch(typeof(CollectionManager), "GetTotalOwnedCount", nameof(Count), new[] { typeof(string) });
            Patch(typeof(CollectionManager), "GetOwnedCount", nameof(CountPremium), new[] { typeof(string), typeof(TAG_PREMIUM) });
            Patch(typeof(CollectionManager), "OnCardsModified", nameof(Grant));
            Patch(typeof(CollectionManager), "UpdateFavoriteHero", nameof(Favorite));
            Patch(typeof(NetCache), "ProcessNewFavoriteCardBack", nameof(FavoriteBack));
            typeof(NativeUIProbe).GetMethod("InitializeCosmetics", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { harmony });
            using (var p = new LocalProfile(path)) p.SelectCardBack(0);
            var serializer = new XmlSerializer(typeof(LocalProfileData));
            LocalProfileData data;
            using (var file = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(file);
            data.GoldBalance = 10000;
            using (var file = File.Create(path)) serializer.Serialize(file, data);
            using (var profile = new LocalProfile(path)) {
                NativeUIProbe.Profile = profile; NativeUIProbe.active = true;
                owned.Clear(); premiumOwned.Clear(); NativeUIProbe.cache.Clear();
                NativeUIProbe.cache[typeof(NetCache.NetCacheCollection)] = new NetCache.NetCacheCollection();
                NativeUIProbe.RefreshCosmetics();
                Check(owned.Count == 11, "Refresh projects always-owned heroes");
                var product = LocalShop.Default.Products.Single(p => p.HeroSkin == "HERO_08a");
                profile.Purchase(product.Id, 1, 1000, "purchase-native");
                NativeUIProbe.RefreshCosmetics(); NativeUIProbe.RefreshCosmetics();
                var collection = (NetCache.NetCacheCollection)NativeUIProbe.cache[typeof(NetCache.NetCacheCollection)];
                Check(owned["HERO_08a"] == 1 && collection.Stacks.Single(s => s.Def.Name == "HERO_08a").Count == 1
                    && collection.TotalCardsOwned == 12, "Post-purchase refresh synchronizes native ownership/cache without replaying grants");
                network = (Network)RuntimeHelpers.GetUninitializedObject(typeof(Network));
                network.SetFavoriteHero(TAG_CLASS.MAGE, new NetCache.CardDefinition { Name = "HERO_08a", Premium = TAG_PREMIUM.NORMAL }, true);
                var favorites = (NetCache.NetCacheFavoriteHeroes)NativeUIProbe.cache[typeof(NetCache.NetCacheFavoriteHeroes)];
                Check(profile.FavoriteHeroSkins["HERO_08"] == "HERO_08a" && favorites.FavoriteHeroes.Count == 11
                    && favorites.FavoriteHeroes.Single(f => f.Item1 == TAG_CLASS.MAGE).Item2.Name == "HERO_08a", "Native request commits and publishes exactly one favourite per class");
                profile.Purchase("cardback-122", 1, 500, "back-native");
                AccessTools.Method(typeof(Network), "SetFavoriteCardBack").Invoke(network, new object[] { 122, true });
                Check(profile.FavoriteCardBacks.SequenceEqual(new[] { 0, 122 }), "Native card-back selection commits ownership-backed choice");
                Check(sortedBack.m_owned, "Purchase refresh updates native cached collection card-back ownership");
                Patch(typeof(Network), "Get", nameof(NetworkInstance));
                Patch(typeof(CardBackManager), "MultipleFavoriteCardBacksEnabled", nameof(MultipleFavorites));
                Patch(typeof(CardBackManager), "GetCardBacks", nameof(BackSnapshot));
                profile.SelectCardBack(0);
                NativeUIProbe.RefreshCosmetics();
                int writes = 0;
                profile.CommitFailure = stage => { if (stage == "write" && ++writes == 2) throw new IOException("second write failed"); };
                backManager.HandleFavoriteToggle(122);
                Check(profile.FavoriteCardBacks.SequenceEqual(new[] { 0, 122 }) && writes == 1, "Native toggle adds requested back in one write");
                profile.CommitFailure = null;
                Check(((NetCache.NetCacheCardBacks)NativeUIProbe.cache[typeof(NetCache.NetCacheCardBacks)]).FavoriteCardBacks.SetEquals(profile.FavoriteCardBacks),
                    "Native favourite set matches ledger after add");
                ((NetCache.NetCacheCardBacks)NativeUIProbe.cache[typeof(NetCache.NetCacheCardBacks)]).FavoriteCardBacks.Remove(122);
                writes = 0;
                profile.CommitFailure = stage => { if (stage == "write") writes++; };
                backManager.HandleFavoriteToggle(122);
                Check(profile.FavoriteCardBacks.SequenceEqual(new[] { 0 }) && writes == 1
                    && ((NetCache.NetCacheCardBacks)NativeUIProbe.cache[typeof(NetCache.NetCacheCardBacks)]).FavoriteCardBacks.SetEquals(profile.FavoriteCardBacks),
                    "Second native toggle uses ledger despite stale cache, removes in one write and refreshes set");
                profile.CommitFailure = null;
                backManager.HandleFavoriteToggle(122);
                byte[] before = File.ReadAllBytes(path);
                profile.CommitFailure = _ => throw new IOException("toggle write failed");
                backManager.HandleFavoriteToggle(0);
                Check(profile.FavoriteCardBacks.SequenceEqual(new[] { 0, 122 }) && File.ReadAllBytes(path).SequenceEqual(before)
                    && ((NetCache.NetCacheCardBacks)NativeUIProbe.cache[typeof(NetCache.NetCacheCardBacks)]).FavoriteCardBacks.SetEquals(new[] { 0, 122 }),
                    "Failed native toggle retains durable, in-memory and displayed previous back");
                profile.CommitFailure = null;
                network.SetFavoriteHero(TAG_CLASS.WARRIOR, new NetCache.CardDefinition { Name = "HERO_08a" }, true);
                AccessTools.Method(typeof(Network), "SetFavoriteCardBack").Invoke(network, new object[] { 9876, true });
                network.SetFavoriteHero(TAG_CLASS.MAGE, new NetCache.CardDefinition { Name = "HERO_08a", Premium = TAG_PREMIUM.GOLDEN }, true);
                Check(File.ReadAllBytes(path).SequenceEqual(before) && !profile.FavoriteHeroSkins.ContainsKey("HERO_01"), "Native requests refuse wrong class, unowned backs and premium variants without transport");
                profile.CommitFailure = _ => throw new IOException("write failed");
                network.SetFavoriteHero(TAG_CLASS.MAGE, new NetCache.CardDefinition { Name = "HERO_08a" }, false);
                Check(File.ReadAllBytes(path).SequenceEqual(before) && profile.FavoriteHeroSkins["HERO_08"] == "HERO_08a", "Native failed selection retains committed favourite");
                profile.CommitFailure = null;
                NativeUIProbe.RefreshCosmetics();
                var backs = (NetCache.NetCacheCardBacks)NativeUIProbe.cache[typeof(NetCache.NetCacheCardBacks)];
                Check(backs.CardBacks.SetEquals(new[] { 0, 122 }) && backs.FavoriteCardBacks.SetEquals(new[] { 0, 122 }), "Back snapshot uses owned ledger set and favourite set");
            }
            using (var reload = new LocalProfile(path)) Check(reload.FavoriteCardBacks.SequenceEqual(new[] { 0, 122 }) && reload.FavoriteHeroSkins["HERO_08"] == "HERO_08a", "Native favourite survives restart");
            PickerMatches(Path.Combine(Path.GetDirectoryName(path), Path.GetFileName(path) + "-picker.xml"));
            Console.WriteLine("PASS native cosmetics requests, ownership refresh, idempotence, refusal and persistence");
        } finally {
            GameDbf.CosmeticCoin = oldCoins;
            harmony.UnpatchAll(harmony.Id); NativeUIProbe.Profile = null; NativeUIProbe.active = false;
            NativeMatch.CosmeticsProfile = null; NativeUIProbe.cache.Clear(); owned.Clear();
            foreach (string file in Directory.GetFiles(Path.GetDirectoryName(path), Path.GetFileName(path) + "*")) File.Delete(file);
        }
    }

    private static void PickerMatches(string path)
    {
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); }
        using var profile = new LocalProfile(path, new ProfileOptions { AllCosmetics = true });
        NativeUIProbe.Profile = profile;
        NativeMatch.CosmeticsProfile = () => profile;
        var dk = LocalDecks.Get("player-core-deathknight");
        var personal = profile.CreateDraft("Skin DK", dk.Hero);
        profile.SaveDraft(personal.Id, personal.Name, dk.Hero, dk.CardIds, personal.SortOrder);
        void Verify(LocalDeck deck, long nativeId, long? personalId, string expected)
        {
            var skin = personalId.HasValue ? profile.DeckCosmetics(personalId.Value).HeroSkin : null;
            var native = new CollectionDeck { ID = nativeId, HeroCardID = skin ?? deck.Hero, HeroOverridden = skin != null };
            foreach (bool favoritesOnly in new[] { false, true })
            {
                native.RandomHeroUseFavorite = favoritesOnly;
                foreach (bool reroll in new[] { false, true, false })
                    Check(native.GetDisplayHeroCardID(reroll) == expected, "picker must resolve saved hero " + deck.Id);
            }
            var messages = new List<PowerHistory>();
            var match = new NativeMatch((id, packet) => { if (packet is PowerHistory history) messages.Add(history); },
                deck, "warrior", 1337) { CosmeticDeckId = personalId };
            match.Start();
            string matchHero = messages.SelectMany(history => history.List).Select(row => row.FullEntity)
                .First(row => row != null && row.Entity == match.Game.Player1.Hero.Id).Name;
            Check(matchHero == expected && native.GetDisplayHeroCardID(false) == matchHero,
                "picker and actual match hero packet differ " + deck.Id);
        }
        Verify(dk, NativeUIProjection.DeckId(dk.Id), null, "HERO_11");
        Verify(dk, personal.Id, personal.Id, "HERO_11");
        profile.SetFavoriteHeroSkin("HERO_11", "HERO_11n");
        profile.SetFavoriteHeroSkin("HERO_10", "HERO_10bm");
        Verify(dk, NativeUIProjection.DeckId(dk.Id), null, "HERO_11n");
        Verify(dk, personal.Id, personal.Id, "HERO_11n");
        var dh = LocalDecks.PlayerCoreStarterDecks.Single(deck => deck.Hero == "HERO_10");
        Verify(dh, NativeUIProjection.DeckId(dh.Id), null, "HERO_10bm");
        profile.SetFavoriteHeroSkin("HERO_08", "HERO_08f");
        Verify(LocalDecks.Get("mage"), NativeUIProjection.DeckId("mage"), null, "HERO_08f");
        foreach (string skin in new[] { "HERO_11a", "HERO_11" })
        {
            profile.SaveDraftCosmetics(personal.Id, personal.Name, new LocalDeckCosmetics { HeroSkin = skin });
            Verify(dk, personal.Id, personal.Id, skin);
        }
        // The editor's unsaved override previews immediately; removing it falls back to the class favourite.
        var edited = new CollectionDeck { ID = personal.Id, HeroCardID = "HERO_11a", HeroOverridden = true };
        Check(edited.GetDisplayHeroCardID(false) == "HERO_11a", "unsaved skin preview");
        edited.HeroOverridden = false;
        Check(edited.GetDisplayHeroCardID(false) == "HERO_11n", "removed skin preview uses favourite");
        profile.SetFavoriteHeroSkin("HERO_11", "HERO_11");
        Verify(dk, NativeUIProjection.DeckId(dk.Id), null, "HERO_11");
        Console.WriteLine("PASS picker/match hero identity: base, favourites, explicit skin/base, core DK/DH, loaner, editor preview, no random rerolls");
    }
}
