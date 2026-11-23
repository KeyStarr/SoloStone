using SabberStoneCore.Enums;
using SabberStoneCore.Model;

internal static class ActiveMetaChecks
{
    internal static void Run()
    {
#if ENGINE_SOURCE
        void Check(bool ok, string message) { if (!ok) throw new Exception("active meta: " + message); }
        var policy = OfflinePoolPolicy.Load();
        Check(!policy.Sets.Contains("BE") && policy.Sets.Count == 6, "release has six pre-BE sets");
        Check(LocalCardCatalog.ActiveMetaNewestSet == "ESCAPEFROM_VIOLET_HOLD"
            && LocalCardCatalog.ActiveMetaExtraCards.Count == 18 + new[] { "BE_036", "BE_EVENT_100", "BE_EVENT_101", "BE_EVENT_102" }.Count(policy.ExtraCards.Contains)
            && LocalCardCatalog.PackEligibleSets.SequenceEqual(policy.Sets.Where(s => s != "CORE")), "explicit pack/release metadata");
        Check(!LocalCardCatalog.InActiveMetaPack(Cards.FromId("CORE_CS2_024"))
            && LocalCardCatalog.InActiveMetaPack(Cards.FromId("JAIL_831")), "pack sets exclude Core and include active expansion");
        Check(LocalDecks.CustomCards.Where(c => policy.Metas.Any(m => m.ExtraCards.Contains(c.Id)))
            .All(c => LocalCardCatalog.InActiveMeta(c) && !LocalCardCatalog.InActiveMetaPack(c)), "collection extras never become pack cards");
        bool OutsideWindow(string id) => id.StartsWith("BE_") && !policy.ExtraCards.Contains(id);
        foreach (var id in new[] { "BE_036", "BE_EVENT_100", "BE_EVENT_101", "BE_EVENT_102" })
            Check(LocalCardCatalog.InActiveMeta(Cards.FromId(id)) == policy.ExtraCards.Contains(id) && !LocalCardCatalog.InActiveMetaPack(Cards.FromId(id)),
                "dated window admission is collection-only: " + id);
        var manifest = policy.BuildManifest();
        foreach (PoolScope scope in Enum.GetValues<PoolScope>())
            Check(!OfflineCardPools.Candidates(manifest, CardClass.MAGE, new OfflinePoolFilter(scope: scope), 0)
                .Any(card => OutsideWindow(card.Id)), "BE cannot leak through " + scope);
        Check(policy.Members.Any(m => m.Set == "BE") && policy.Members.Where(m => m.Set == "BE").All(m => m.Scope == (policy.ExtraCards.Contains(m.CardId) ? "Standard" : "Inactive")),
            "BE evidence retained outside active pools");
        Check(!policy.AllowsCard(Cards.FromId("BE_152")) && policy.AllowsCard(Cards.FromId("JAIL_831")),
            "set identities govern player eligibility independently of pool admissions");
        int coreSet = (int)Cards.FromId("CORE_CS2_024").Set;
        Check(NativeUIProjection.DisplayableSetIds(new[] { 2, (int)Cards.FromId("BE_152").Set, coreSet, coreSet })
            .SequenceEqual(new[] { 2, coreSet }), "collection set filters retain Classic/Core and hide inactive BE");
        Check(LocalDecks.All.SelectMany(deck => deck.CardIds).Concat(LocalCoreProgression.Unlocks.Select(c => c.Id))
            .Concat(LocalDecks.CustomCards.Select(c => c.Id)).All(id => !OutsideWindow(id)), "player catalogs and NPCs exclude BE outside window admissions");
        Check(NativeUIProjection.ProjectedLoanerKeys(0).SequenceEqual(NativeUIProjection.PlayerLoanerKeys)
            && NativeUIProjection.PlayerLoanerKeys.Count == 11, "default tray contains only player loaners");
        Check(NativeUIProjection.ProjectedLoanerKeys(0, "showcase").SelectMany(key => LocalDecks.Get(key).CardIds)
            .All(id => LocalCardCatalog.InActiveMeta(Cards.FromId(id))), "showcase selection uses only active player decks");
        Check(LocalDecks.TestLoaners.SelectMany(deck => deck.CardIds).Any(id => id.StartsWith("BE_")), "BE test recipes retained");
        string path = Path.Combine(Path.GetTempPath(), "hs-active-meta-" + Guid.NewGuid(), "profile.xml");
        try
        {
            using (var profile = new LocalProfile(path))
            {
                Check(profile.Collection.Keys.All(id => !OutsideWindow(id)), "fresh ownership excludes BE outside window admissions");
                Check(policy.ExtraCards.All(id => !profile.Collection.ContainsKey(id)), "fresh ownership excludes permanent EVENT cards");
                Check(LocalDecks.KnownBundleAndSideboardCards.All(card => !profile.Collection.ContainsKey(card.Id)),
                    "fresh ownership has no virtual Fabled or sideboard cards (owner 2026-10-30)");
                bool rejected = false;
                try { LocalDecks.Draft("Inactive", "HERO_09", new[] { "BE_193" }); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "inactive cards cannot enter custom decks");
                rejected = false;
                try { LocalDecks.Draft("Hunter", "HERO_05", new[] { "JAIL_831" }).WithSideboards(new[] {
                    new LocalSideboard { Source = "JAIL_831", Cards = new() { "BE_855" } } }); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "inactive cards cannot enter custom sideboards");
                var draft = profile.CreateDraft("Historical hunter", "HERO_05");
                profile.SaveDraft(draft.Id, draft.Name, draft.Hero, new[] { "JAIL_831" }, draft.SortOrder);
            }
            // A previously saved BE sideboard survives reload, but cannot be exposed or played.
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(LocalProfileData));
            LocalProfileData saved;
            using (var input = File.OpenRead(path)) saved = (LocalProfileData)serializer.Deserialize(input);
            saved.CustomDecks.Single().Sideboards.Add(new LocalSideboard { Source = "JAIL_831", Cards = new() { "BE_855" } });
            using (var output = File.Create(path)) serializer.Serialize(output, saved);
            byte[] before = File.ReadAllBytes(path);
            using (var profile = new LocalProfile(path))
            {
                Check(profile.Collection.Keys.All(id => !OutsideWindow(id)), "reloaded ownership excludes BE outside window admissions");
                Check(profile.CustomDecks.Count == 0, "historical inactive sideboard stays hidden");
            }
            Check(File.ReadAllBytes(path).SequenceEqual(before), "inactive sideboard reload preserves saved bytes");
        }
        finally { Directory.Delete(Path.GetDirectoryName(path), true); }
        for (int seed = 0; seed < 50; seed++)
            Check(LocalEconomy.GeneratePack(new Dictionary<string, int>(), seed, 0).Cards.All(c => !c.Id.StartsWith("BE_")),
                "local packs exclude BE");
        Console.WriteLine("PASS active meta: player catalogs, NPCs, all generation scopes, packs, ownership/reload, explicit BE harness");
#endif
    }
}

// #87 (owner 2026-11-15): Classic and Basic cards are Wild. One engine rule (OfflinePoolPolicy.IsStandard) decides Standard
// deck legality, the native client's format answers, new-profile ownership and the play gate.
internal static class ClassicWildChecks
{
    internal static void Run()
    {
#if ENGINE_SOURCE
        void Check(bool ok, string message) { if (!ok) throw new Exception("classic wild: " + message); }
        var policy = OfflinePoolPolicy.Load();
        foreach (string id in new[] { "CS2_024", "CS2_182", "EX1_007", "EX1_559", "CS2_179", "NEW1_011", "DS1_055" })
            Check(!LocalCardCatalog.IsStandardId(id) && LocalCardCatalog.InActiveMeta(Cards.FromId(id)), "Classic/Basic " + id + " is Wild but still collectible");
        foreach (string id in new[] { "CORE_CS2_024", "CORE_EX1_559", "CS3_024", "RLK_503", "JAIL_831", "CATA_565", "EDR_950", "TIME_EVENT_999" })
            Check(LocalCardCatalog.IsStandardId(id), id + " is Standard");
        Check(!LocalCardCatalog.IsStandardId("BE_152") && LocalCardCatalog.IsStandardId("BE_036") == policy.ExtraCards.Contains("BE_036"),
            "an unreleased set is not Standard; its admitted extra follows the window");
        Check(LocalCardCatalog.Current.All(card => !LocalCardCatalog.IsStandard(card)), "the whole Classic ledger is Wild");
        Check(LocalCoreProgression.Unlocks.All(unlock => LocalCardCatalog.IsStandardId(unlock.Id)), "every Core unlock is Standard");
        Check(LocalDecks.PlayerCoreStarterDecks.Count == 11 && LocalDecks.PlayerCoreStarterDecks.SelectMany(deck => deck.CardIds).All(LocalCardCatalog.IsStandardId),
            "starter loaners are Standard");
        Check(LocalDecks.NpcDecks.SelectMany(deck => deck.CardIds).All(LocalCardCatalog.IsStandardId), "showcase NPC decks are Standard");
        // Every shipped Standard deck (NPC ranked and casual libraries, shop recipes incl. sideboards) holds only Standard ids;
        // Holy Light and Truesilver Champion (CS2_089, CS2_097: Basic/Classic, Core printings hidden in 37.0) are Wild.
        string root = RepoRoot.Find("data/npc/decks-v1.json") ?? throw new Exception("classic wild: repo root");
        IEnumerable<string> Ids(System.Text.Json.JsonElement deck) =>
            deck.GetProperty("cards").EnumerateArray().Select(row => row.GetProperty("id").GetString()!)
                .Concat(deck.TryGetProperty("sideboards", out var boards) ? boards.EnumerateArray().SelectMany(board =>
                    board.GetProperty("cards").EnumerateArray().Select(row => row.GetProperty("id").GetString()!)) : Enumerable.Empty<string>());
        foreach (var (file, key) in new[] { ("data/npc/decks-v1.json", "decks"), ("data/npc/casual-decks-v1.json", "decks"), ("data/shop/recipes-v1.json", "recipes") })
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root, file)));
            foreach (var deck in doc.RootElement.GetProperty(key).EnumerateArray())
            {
                var wild = LocalCardCatalog.NonStandardIds(Ids(deck));
                Check(wild.Count == 0, file + " " + deck.GetProperty("id").GetString() + " holds Wild cards: " + string.Join(",", wild));
            }
        }
        foreach (string id in new[] { "CORE_CS2_231", "CORE_FP1_011", "CORE_VAN_EX1_572", "CORE_EDR_004" })
        {
            SabberStoneCore.Model.Card hidden = null;
            try { hidden = Cards.FromId(id); } catch (KeyNotFoundException) { }
            Check(!LocalCardCatalog.IsStandardId(id) && !LocalShop.IsStandardCard(hidden), "a CORE_ prefix alone is not Standard: " + id);
        }
        Check(LocalProfile.KnownLedgerCard("CORE_EDR_004") && LocalProfile.KnownLedgerCard("CORE_FP1_011") && !LocalProfile.KnownLedgerCard("CORE_NOT_A_CARD"),
            "a non-Standard collectible CORE_ id an older save may hold stays known to the ledger");
        Check(NativeUIProjection.NativeCardIsWild(true, "BE_152") == null, "an unreleased set's card is left to the client, like its set");
        Check(LocalShop.IsStandardCard(Cards.FromId("CORE_EDR_004_2026")) && LocalShop.IsStandardCard(Cards.FromId("CORE_CS2_024"))
            && LocalShop.IsStandardCard(Cards.FromId("JAIL_831")) && !LocalShop.IsStandardCard(Cards.FromId("CS2_024")),
            "the shop's Standard check is the engine rule");
        Check(!LocalCardCatalog.IsStandardId("CS2_089") && !LocalCardCatalog.IsStandardId("CS2_097")
            && !LocalProfile.ClassicReprints.ContainsKey("CS2_089") && !LocalProfile.ClassicReprints.ContainsKey("CS2_097"),
            "Holy Light and Truesilver Champion are Wild with no Core reprint");
        Check(LocalCardCatalog.NonStandardIds(new[] { "CS2_182", "CORE_CS2_024", "CS2_182", "EX1_007" }).SequenceEqual(new[] { "CS2_182", "EX1_007" }),
            "non-Standard ids: deck order, each once");
        // Native answers (NativeFormatWild): card-level from the engine rule; set-level rotates every set but the active ones.
        int coreSet = (int)Cards.FromId("CORE_CS2_024").Set, violetHold = (int)Cards.FromId("JAIL_831").Set, be = (int)Cards.FromId("BE_152").Set;
        Check(NativeUIProjection.NativeCardIsWild(true, "CS2_024") == true && NativeUIProjection.NativeCardIsWild(true, "CORE_CS2_024") == false
            && NativeUIProjection.NativeCardIsWild(false, "CS2_024") == null && NativeUIProjection.NativeCardIsWild(true, "CS2_boar") == null
            && NativeUIProjection.NativeCardIsWild(true, "NOT_A_CARD") == null, "native card format: Classic Wild, Core Standard, tokens and inactive untouched");
        Check(NativeUIProjection.NativeSetIsRotated(true, 2) == true && NativeUIProjection.NativeSetIsRotated(true, 3) == true
            && NativeUIProjection.NativeSetIsRotated(true, 4) == true && NativeUIProjection.NativeSetIsRotated(true, NativeUIProjection.NativeLegacySet) == true
            && NativeUIProjection.NativeSetIsRotated(true, 1130) == true,
            "native sets: Basic, Classic, Hall of Fame, Legacy and old expansions rotate");
        Check(NativeUIProjection.NativeSetIsRotated(true, coreSet) == false && NativeUIProjection.NativeSetIsRotated(true, violetHold) == false
            && NativeUIProjection.NativeSetIsRotated(true, NativeUIProjection.NativeEventSet) == false
            && NativeUIProjection.NativeSetIsRotated(true, 0) == null && NativeUIProjection.NativeSetIsRotated(true, be) == null
            && NativeUIProjection.NativeSetIsRotated(false, 3) == null, "native sets: active, EVENT stay Standard; INVALID, unreleased, inactive untouched");
        Check(NativeUIProjection.NativeCardIsWild(true, "HERO_08") == null && NativeUIProjection.NativeCardIsWild(true, "HERO_08a") == null
            && NativeUIProjection.NativeCardIsWild(true, "CORE_EX1_323") == null, "heroes, hero skins and hero cards keep the client's answer");
        Check(NativeUIProjection.NativeSetIsRotated(true, (int)Cards.FromId("HERO_08a").Set) == null, "the hero skin set is not a card set and never rotates");
        foreach (var pair in LocalProfile.ClassicReprints)
            Check(!LocalCardCatalog.IsStandardId(pair.Key) && LocalCardCatalog.IsStandardId(pair.Value) && LocalCoreProgression.IsAvailable(pair.Value)
                && Cards.FromId(pair.Key).Class == Cards.FromId(pair.Value).Class && LocalCardCatalog.CopyGroup(pair.Value) == pair.Key
                && pair.Value.StartsWith("CORE_", StringComparison.OrdinalIgnoreCase), "Classic reprint pair " + pair.Key + " -> " + pair.Value);
        Check(LocalProfile.ClassicReprints.Count >= 50, "Classic reprint map covers the Classic ledger's Core reprints");
        Check(NativeUIProjection.NativeFallbackSet(true, "CS2_024") == NativeUIProjection.NativeLegacySet
            && NativeUIProjection.NativeFallbackSet(true, "CORE_CS2_024") == null && NativeUIProjection.NativeFallbackSet(false, "CS2_024") == null,
            "a set-less Wild card shows as Legacy; a set-less Core card keeps INVALID");
        string path = Path.Combine(Path.GetTempPath(), "hs-classic-wild-" + Guid.NewGuid(), "profile.xml");
        try
        {
            using var profile = new LocalProfile(path);
            Check(profile.Collection.Keys.All(LocalCardCatalog.IsStandardId), "a new profile owns no Wild card");
            var commons = LocalCoreProgression.Unlocks.Where(unlock => Cards.FromId(unlock.Id).Rarity == Rarity.COMMON).ToArray();
            Check(commons.Length > 150 && commons.All(unlock => profile.Collection.TryGetValue(unlock.Id, out int count) && count == unlock.Copies),
                "a new profile owns every Core common, class and neutral");
            Check(LocalCoreProgression.Unlocks.Where(unlock => unlock.Threshold > 1).All(unlock => !profile.Collection.ContainsKey(unlock.Id))
                && LocalCoreProgression.Unlocks.Where(unlock => unlock.Hero.Length != 0 && Cards.FromId(unlock.Id).Rarity != Rarity.COMMON)
                    .Count(unlock => profile.Collection.ContainsKey(unlock.Id)) == 3,
                "rarer Core cards still unlock by level, except the rares that bring Mage and Shaman to nine");
            foreach (string hero in LocalCoreProgression.Heroes)
                Check(LocalCoreProgression.Unlocks.Count(unlock => unlock.Hero == hero && profile.Collection.ContainsKey(unlock.Id)) >= 9, "at least 9 class cards at start: " + hero);
            var draft = profile.CreateDraft("Old mage", "HERO_08");
            profile.SaveDraft(draft.Id, draft.Name, draft.Hero, new[] { "CORE_CS2_024", "CORE_CS2_024" }, draft.SortOrder);
            Check(profile.WildCards(draft.Id).Count == 0, "a Core draft holds no Wild card");
            var paladin = profile.CreateDraft("Old paladin", "HERO_04");
            profile.SaveDraft(paladin.Id, paladin.Name, paladin.Hero, new[] { "CS2_089", "CS2_097", "CORE_ICC_038" }, paladin.SortOrder);
            Check(profile.WildCards(paladin.Id).SequenceEqual(new[] { "CS2_089", "CS2_097" }) && !profile.IsDeckComplete(paladin.Id)
                && profile.CustomDecks.Any(deck => deck.Id == paladin.Id),
                "a saved deck with Holy Light and Truesilver stays listed, flagged Wild and unplayable");
        }
        finally { Directory.Delete(Path.GetDirectoryName(path), true); }
        Console.WriteLine("PASS classic wild: Standard rule, native format answers, new-profile Core commons, no Wild ownership");
#endif
    }
}
