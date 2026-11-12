// Compile the production pet mapping and cache projection; only client DBF/cache boundaries are doubled.
partial class NativePetsHarness
{
    internal sealed class PetVariantDbfRecord {
        internal int ID, PetId, CardId; internal bool Enabled = true;
    }
    private sealed class Parent { internal int ID; internal bool Enabled = true; }
    private sealed class PetLevel { internal int PetId, LevelNumber, PetVariantUnlocked, NextXpNeeded; public int Level => LevelNumber; }
    private sealed class Table<T>(List<T> rows) {
        internal List<T> GetRecords() => rows;
        internal T GetRecord(int id) => rows.FirstOrDefault(row => (int)row.GetType().GetField("ID", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(row) == id);
    }
    private static class GameDbf {
        internal static Table<Parent> Pet;
        internal static Table<PetLevel> PetLevel;
        internal static Table<PetVariantDbfRecord> PetVariant;
    }
    private static class GameUtils { internal static int TranslateCardIdToDbId(string card) => global::GameUtils.TranslateCardIdToDbId(card); internal static string TranslateDbIdToCardId(int id) => global::GameUtils.TranslateDbIdToCardId(id); }
    private sealed class NetCache {
        internal sealed class PetInfo {
            internal int PetId, CurrentLevel, CurrentXp; internal bool IsHsFavorite, IsBgFavorite;
            internal HashSet<int> VariantIDs = new();
        }
        internal sealed class PetVariantInfo { internal int PetVariantId; internal bool IsHsFavorite, IsBgFavorite; }
        internal sealed class NetCachePets {
            internal Dictionary<int, PetInfo> Pets = new(); internal Dictionary<int, PetVariantInfo> PetVariants = new();
        }
        internal delegate void DelPetsXpLevelChanged(int pet, int beforeLevel, int level, int beforeXp, int xp);
        internal DelPetsXpLevelChanged PetsXpLevelChanged;
        internal delegate void DelFavoritePetChangedListener(int id, bool hs, bool bg);
        internal delegate void DelFavoritePetVariantChangedListener(int id, bool hs, bool bg);
        internal DelFavoritePetChangedListener FavoritePetChanged;
        internal DelFavoritePetVariantChangedListener FavoritePetVariantChanged;
        internal static readonly NetCache Instance = new(); internal static NetCache Get() => Instance;
        internal int FavouriteChanges;
        internal void ProcessFavoritePet(int id, bool? hs, bool? bg) {
            var row = ((NetCachePets)cache[typeof(NetCachePets)]).Pets[id];
            if (hs.HasValue && hs.Value != row.IsHsFavorite) { row.IsHsFavorite=hs.Value; FavoritePetChanged?.Invoke(id,row.IsHsFavorite,row.IsBgFavorite); }
        }
        internal void ProcessFavoritePetVariant(int id, bool? hs, bool? bg) {
            var row = ((NetCachePets)cache[typeof(NetCachePets)]).PetVariants[id];
            if (hs.HasValue && hs.Value != row.IsHsFavorite) { row.IsHsFavorite=hs.Value; FavoritePetVariantChanged?.Invoke(id,row.IsHsFavorite,row.IsBgFavorite); }
        }
    }
    private static class AccessTools {
        internal static System.Reflection.FieldInfo Field(Type type, string name) => type.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
    }
    private static bool PendingSilentPets;
    private static bool IsolatedLaunch = true, active = true, ready = true, failPublish, savedOnFailure;
    private static void CosmeticSelectionFailed(Exception error, bool saved) { savedOnFailure = saved; }
    private static LocalProfile Profile;
    private static readonly Dictionary<Type, object> cache = new();
    private static void Publish(object value) { if (failPublish) throw new IOException("native refresh fault"); cache[value.GetType()] = value; }
    internal static void Run(Action<bool, string> check, Func<string, int, string> fresh) {
        NetCache.Instance.FavouriteChanges = 0;
        NetCache.Instance.FavoritePetChanged = (id, hs, bg) => NetCache.Instance.FavouriteChanges++;
        NetCache.Instance.FavoritePetVariantChanged = (id, hs, bg) => NetCache.Instance.FavouriteChanges++;
        var parent = new Parent { ID = 73 };
        var variants = LocalShop.Default.FormsForPet("PET_3_1").Select((card, index) => new PetVariantDbfRecord {
            ID = 500 + index * 7, PetId = parent.ID, CardId = GameUtils.TranslateCardIdToDbId(card) }).ToList();
        var levels = variants.Select((row, i) => new PetLevel { PetId = parent.ID, LevelNumber = i + 1,
            PetVariantUnlocked = row.ID, NextXpNeeded = new[] { 25, 50, 75, 0 }[i] }).ToList();
        GameDbf.Pet = new(new List<Parent> { parent }); GameDbf.PetVariant = new(variants); GameDbf.PetLevel = new(levels);
        check(PetEntryValid(LocalShop.Default.Product("pet-PET_3_1")), "native admission validates explicit family, variants and native thresholds");
        levels[0].NextXpNeeded++;
        check(!PetEntryValid(LocalShop.Default.Product("pet-PET_3_1")), "native threshold mismatch excludes stale offer");
        levels[0].NextXpNeeded--;
        check(CosmeticPets().Pets.Count == 0, "native pets projection tolerates pre-bind null profile");
        using var profile = new LocalProfile(fresh("pet-native-projection", 5000)); Profile = profile;
        profile.Purchase("pet-PET_3_1", 1, 5000, "native-pet");
        var projected = CosmeticPets();
        check(projected.Pets.Count == 1 && projected.Pets[73].CurrentLevel == 1 && projected.Pets[73].CurrentXp == 0
            && projected.Pets[73].VariantIDs.SetEquals(new[] { 500 }) && projected.PetVariants.Keys.SequenceEqual(new[] { 500 }),
            "native purchase projects parent first and only unlocked base variant");
        RefreshPets(); check(NetCache.Instance.FavouriteChanges == 2, "pet purchase sends native parent and form favourite events"); int xpEvents = 0;
        NetCache.Instance.PetsXpLevelChanged = (id, beforeLevel, level, beforeXp, xp) => { check(id == 73 && beforeLevel == 1 && level == 2 && beforeXp == 0 && xp == 0, "native XP event carries old/new saved values"); xpEvents++; };
        var match = profile.Begin("mage", "priest", 91); profile.Settle(match.Id, "Lost", 4, false); profile.AcknowledgeRewardPresentation(match.Id);
        profile.SelectPet("PET_3_2"); projected = CosmeticPets();
        check(projected.Pets[73].CurrentLevel == 2 && projected.Pets[73].CurrentXp == 0 && projected.Pets[73].VariantIDs.SetEquals(new[] { 500,507 })
            && projected.PetVariants[507].IsHsFavorite && !projected.PetVariants[500].IsHsFavorite,
            "native growth projects exact level/remainder/unlocked variants and chosen form favourite");
        RefreshPets(); int changes = NetCache.Instance.FavouriteChanges; RefreshPets();
        check(xpEvents == 1 && changes == 4 && NetCache.Instance.FavouriteChanges == changes, "pet refresh cannot duplicate favourite change notifications");
        // The end-of-game pet moment publishes the settled ledger silently, then fires its own single event from the receipt.
        // If that silent publish fails, the next refresh of any kind must stay silent instead of firing the settled move.
        var silent = profile.Begin("mage", "priest", 92); profile.Settle(silent.Id, "Lost", 4, false); profile.AcknowledgeRewardPresentation(silent.Id);
        PendingSilentPets = true; failPublish = true;
        try { RefreshPets(false); check(false, "silent publish fault must surface"); } catch (IOException) { }
        failPublish = false;
        RefreshPets();
        check(xpEvents == 1 && !PendingSilentPets && ((NetCache.NetCachePets)cache[typeof(NetCache.NetCachePets)]).Pets[73].CurrentXp == profile.PetLevelXp("PET_3_1"),
            "after a failed silent publish the next refresh publishes the settled XP without the XP event");
        RefreshPets(false); RefreshPets();
        check(xpEvents == 1, "a later refresh cannot re-fire an already published pet XP move");
        profile.SelectPet(null); RefreshPets();
        projected = (NetCache.NetCachePets)cache[typeof(NetCache.NetCachePets)];
        check(NetCache.Instance.FavouriteChanges == 6 && projected.Pets.Count == 1 && projected.PetVariants.Count == 2 && projected.Pets.Values.All(p => !p.IsHsFavorite)
            && projected.PetVariants.Values.All(p => !p.IsHsFavorite), "None clears favourites while preserving ownership and unlocked variants");
        failPublish = true; savedOnFailure = false;
        SelectLocalPetVariant(507, true, null);
        check(savedOnFailure && profile.SelectedPet == "PET_3_2", "saved pet form remains saved when native refresh fails");
        failPublish = false; savedOnFailure = true;
        SelectLocalPetVariant(521, true, null);
        check(!savedOnFailure && profile.SelectedPet == "PET_3_2", "rejected locked pet form reports no saved selection");
        failPublish = true; savedOnFailure = false;
        SelectLocalPet(73, true, null);
        check(savedOnFailure && profile.SelectedPet == "PET_3_1", "saved pet family remains saved when native refresh fails");
        failPublish = false;
        Profile = null; cache.Clear();
    }
}
