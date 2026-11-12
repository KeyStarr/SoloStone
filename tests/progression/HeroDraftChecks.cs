using System.Xml.Serialization;

internal static class HeroDraftChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    internal static void Run(string path, bool create)
    {
        RunHero(path, create, "HERO_10", "DH");
        RunHero(path + ".dk", create, "HERO_11", "DK");
#if ENGINE_SOURCE
        if (!create) Console.WriteLine("PASS SourceVendorSourcePreservesDkDraft");
#endif
    }
    private static void RunHero(string path, bool create, string hero, string label)
    {
#if ENGINE_SOURCE
        bool source = true;
#else
        bool source = false;
#endif
        var serializer = new XmlSerializer(typeof(LocalProfileData));
        LocalProfileData Read() {
            using var stream = File.OpenRead(path);
            return (LocalProfileData)serializer.Deserialize(stream);
        }
        void Write(LocalProfileData saved) {
            using var stream = File.Create(path);
            serializer.Serialize(stream, saved);
        }
        if (create)
        {
            using (var profile = new LocalProfile(path)) {
                profile.CreateDraft("Empty " + label, source ? hero : "HERO_08");
                var full = profile.CreateDraft("Full " + label, source ? hero : "HERO_08");
                var neutrals = profile.Collection.Where(pair => pair.Value >= 2
                    && SabberStoneCore.Model.Cards.FromId(pair.Key).Class == SabberStoneCore.Enums.CardClass.NEUTRAL
                    && !LocalCardCatalog.IsCore(pair.Key)).Take(15).SelectMany(pair => new[] { pair.Key, pair.Key }).ToArray();
                Check(neutrals.Length == 30, "fixture needs 30 owned neutral cards");
                if (source && label == "DK")
                    neutrals[0] = LocalCoreProgression.Unlocks.First(card => card.Hero == hero && card.Threshold == 1).Id;
                profile.SaveDraft(full.Id, full.Name, full.Hero, neutrals, full.SortOrder);
                profile.CreateDraft("Mage", "HERO_08");
            }
            // Vendor's ordinary suite also exercises loading a source-shaped save.
            if (!source) {
                var saved = Read();
                saved.CustomDecks.Take(2).ToList().ForEach(deck => deck.Hero = hero);
                if (label == "DK")
                    saved.CustomDecks[1].Cards[0] = LocalCoreProgression.Unlocks.First(card => card.Hero == hero && card.Threshold == 1).Id;
                Write(saved);
            }
        }
        byte[] before = File.ReadAllBytes(path);
        using (var profile = new LocalProfile(path)) {
            Check(profile.CustomDecks.Select(deck => deck.Name).SequenceEqual(source
                ? new[] { "Empty " + label, "Full " + label, "Mage" } : new[] { "Mage" }), "unavailable hero draft visibility");
            foreach (var draft in Read().CustomDecks.Take(2))
                Check(LocalDecks.ValidateStoredDraft(draft.Name, draft.Hero, draft.Cards) == source,
                    "stored hero draft usability");
            if (!source) {
                bool rejected = false;
                try { profile.CreateDraft("Unavailable " + label, hero); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "strict create accepted unavailable hero");
            }
        }
        Check(File.ReadAllBytes(path).SequenceEqual(before), "hero draft load changed saved bytes");
        var invalid = Read();
        invalid.CustomDecks[0].Hero = "HERO_99";
        string invalidPath = path + ".invalid";
        using (var stream = File.Create(invalidPath)) serializer.Serialize(stream, invalid);
        bool invalidRejected = false;
        try { using var ignored = new LocalProfile(invalidPath); }
        catch (InvalidOperationException) { invalidRejected = true; }
        Check(invalidRejected, "stored invalid hero draft accepted");
        Check(File.ReadAllBytes(path).SequenceEqual(before), "invalid hero check changed original save");
        if (!source) {
            string limitPath = path + ".limit";
            File.WriteAllBytes(limitPath, before);
            using (var profile = new LocalProfile(limitPath)) {
                for (int n = 1; n < LocalProfile.MaximumCustomDecks; n++) profile.CreateDraft("Mage " + n, "HERO_08");
                Check(profile.CustomDecks.Count == LocalProfile.MaximumCustomDecks, "hidden hero drafts consume visible slots");
                profile.ReorderDrafts(profile.CustomDecks.Reverse().Select(deck => deck.Id));
                bool rejected = false;
                try { profile.CreateDraft("Overflow", "HERO_08"); }
                catch (InvalidOperationException) { rejected = true; }
                Check(rejected, "draft past the visible limit accepted");
            }
            using var restarted = new LocalProfile(limitPath);
            Check(restarted.CustomDecks.Count == LocalProfile.MaximumCustomDecks, "hidden hero extras block restart");
            using var stream = File.OpenRead(limitPath);
            var saved = (LocalProfileData)serializer.Deserialize(stream);
            Check(saved.CustomDecks.Count == LocalProfile.MaximumCustomDecks + 2 && saved.CustomDecks.Take(2).All(deck => deck.Hero == hero)
                && saved.CustomDecks[0].Cards.Count == 0 && saved.CustomDecks[1].Cards.SequenceEqual(Read().CustomDecks[1].Cards),
                "legitimate commits lost hidden hero drafts");
        }
        if (source && !create)
        {
            Check(File.Exists(path + ".limit"), "missing vendor-committed hero draft artifact");
            byte[] committedBytes = File.ReadAllBytes(path + ".limit");
            using (var restored = new LocalProfile(path + ".limit"))
                foreach (var original in Read().CustomDecks.Take(2))
                {
                    var draft = restored.GetDraft(original.Id);
                    Check(restored.CustomDecks.Any(visible => visible.Id == original.Id)
                        && draft.Hero == hero && draft.Cards.SequenceEqual(original.Cards),
                        "source lost hero draft after vendor commits/reorder");
                }
            Check(File.ReadAllBytes(path + ".limit").SequenceEqual(committedBytes), "source rewrote vendor-committed profile");
        }
        Console.WriteLine("PASS saved hero drafts preserved; " + (source ? "source visible" : "vendor hidden"));
    }
}
