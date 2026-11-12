using System.Xml.Serialization;

internal static class HeroDraftChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    internal static void Run(string path, bool create)
    {
        RunHero(path, create, "HERO_10", "DH");
        RunHero(path + ".dk", create, "HERO_11", "DK");
    }
    private static void RunHero(string path, bool create, string hero, string label)
    {
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
                profile.CreateDraft("Empty " + label, hero);
                var full = profile.CreateDraft("Full " + label, hero);
                var neutrals = profile.Collection.Where(pair => pair.Value >= 2
                    && SabberStoneCore.Model.Cards.FromId(pair.Key).Class == SabberStoneCore.Enums.CardClass.NEUTRAL
                    && !LocalCardCatalog.IsCore(pair.Key)).Take(15).SelectMany(pair => new[] { pair.Key, pair.Key }).ToArray();
                Check(neutrals.Length == 30, "fixture needs 30 owned neutral cards");
                if (label == "DK")
                    neutrals[0] = LocalCoreProgression.Unlocks.First(card => card.Hero == hero && card.Threshold == 1).Id;
                profile.SaveDraft(full.Id, full.Name, full.Hero, neutrals, full.SortOrder);
                profile.CreateDraft("Mage", "HERO_08");
            }
        }
        byte[] before = File.ReadAllBytes(path);
        using (var profile = new LocalProfile(path)) {
            Check(profile.CustomDecks.Select(deck => deck.Name).SequenceEqual(
                new[] { "Empty " + label, "Full " + label, "Mage" }), "hero draft visibility");
            foreach (var draft in Read().CustomDecks.Take(2))
                Check(LocalDecks.ValidateStoredDraft(draft.Name, draft.Hero, draft.Cards),
                    "stored hero draft usability");
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
        Console.WriteLine("PASS saved hero drafts preserved");
    }
}
