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
