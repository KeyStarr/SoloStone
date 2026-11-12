using System.Reflection;
using System.Xml.Linq;
using System.Xml.Serialization;

internal static class FavoriteCardBackTests
{
    internal static void Run(Action<bool, string> check, Action<Action, string> reject, Func<string, int, string> fresh)
    {
        using var fixture = new CosmeticsFixture();
        var serializer = new XmlSerializer(typeof(LocalProfileData));
        using (var xml = new StringWriter()) {
            serializer.Serialize(xml, new LocalProfileData { SelectedCardBack = 122 });
            check(!xml.ToString().Contains("SelectedCardBack"), "XmlSerializer never writes a populated legacy scalar");
        }
        string path = fresh("favorite-backs", 10000);
        using (var profile = new LocalProfile(path)) {
            profile.Purchase("cardback-122", 1, 500, "favorite-back");
            profile.SetCardBackFavorite(122, true);
            check(profile.FavoriteCardBacks.SequenceEqual(new[] { 0, 122 }), "favourite add preserves sorted set");
            var copy = (LocalProfileData)typeof(LocalProfile).GetMethod("Snapshot", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(profile, null)!;
            copy.FavoriteCardBacks.Clear();
            ((int[])profile.FavoriteCardBacks)[0] = 122;
            check(profile.FavoriteCardBacks.SequenceEqual(new[] { 0, 122 }), "snapshot and public favourites are detached");
            profile.CommitFailure = _ => throw new IOException("unexpected write");
            profile.SetCardBackFavorite(122, true);
            check(profile.FavoriteCardBacks.Count == 2, "redundant add does not commit");
            profile.CommitFailure = null;
            reject(() => profile.SetCardBackFavorite(9876, true), "unowned favourite add rejected");
            reject(() => profile.SetCardBackFavorite(9876, false), "unowned favourite remove rejected");
            profile.CommitFailure = stage => { if (stage == "write") throw new IOException("failed write"); };
            reject(() => profile.SetCardBackFavorite(122, false), "favourite remove write failure");
            check(profile.FavoriteCardBacks.SequenceEqual(new[] { 0, 122 }), "failed remove keeps committed list");
            profile.CommitFailure = null;
            var reached = new HashSet<int>();
            for (int seed = 0; seed < 100; seed++) {
                int back = profile.MatchCardBack(new Random(seed)); reached.Add(back);
                check(back == profile.MatchCardBack(new Random(seed)) && profile.FavoriteCardBacks.Contains(back), "seeded favourites deterministic and bounded");
                check(profile.DeckCardBack(-1, new Random(seed)) == back, "missing deck draws same seeded favourite");
            }
            check(reached.SetEquals(profile.FavoriteCardBacks), "every favourite reachable across seeds");
            long id = profile.CreateDraft("Favourite back", "HERO_08").Id;
            check(profile.DeckCardBack(id, new Random(1)) == profile.MatchCardBack(new Random(1)), "deck without own back uses favourites");
            bool nullMatch = false, nullDeck = false;
            try { profile.MatchCardBack(null!); } catch (ArgumentNullException) { nullMatch = true; }
            try { profile.DeckCardBack(id, null!); } catch (ArgumentNullException) { nullDeck = true; }
            check(nullMatch && nullDeck, "card-back random is required");
        }
        using (var profile = new LocalProfile(path)) {
            check(profile.FavoriteCardBacks.SequenceEqual(new[] { 0, 122 }), "multiple favourites XML round-trip");
            profile.SetCardBackFavorite(0, false);
            check(profile.FavoriteCardBacks.SequenceEqual(new[] { 122 }) && Enumerable.Range(0, 100).All(seed => profile.MatchCardBack(new Random(seed)) == 122), "single favourite stable across seeds");
            profile.SetCardBackFavorite(122, false);
            check(profile.FavoriteCardBacks.SequenceEqual(new[] { 0 }), "removing last favourite restores Classic");
            profile.CommitFailure = _ => throw new IOException("unexpected write");
            profile.SetCardBackFavorite(122, false); profile.SetCardBackFavorite(0, false);
            profile.CommitFailure = null;
            profile.SelectCardBack(122);
        }
        var saved = XDocument.Load(path);
        check(saved.Root!.Element("SelectedCardBack") == null, "new XML never writes legacy scalar");
        saved.Root.Element("FavoriteCardBacks")!.Remove();
        saved.Root.Add(new XElement("SelectedCardBack", 122)); saved.Save(path);
        using (var profile = new LocalProfile(path)) {
            check(profile.FavoriteCardBacks.SequenceEqual(new[] { 122 }), "legacy scalar loads as favourite");
            profile.SelectCardBack(122);
        }
        check(XDocument.Load(path).Root!.Element("SelectedCardBack") == null, "legacy scalar removed on rewrite");
        saved = XDocument.Load(path); saved.Root!.Element("FavoriteCardBacks")!.Remove(); saved.Save(path);
        using (var profile = new LocalProfile(path)) check(profile.FavoriteCardBacks.SequenceEqual(new[] { 0 }), "missing favourites and legacy scalar default to Classic");
        var empty = new XDocument(saved); empty.Root!.Add(new XElement("FavoriteCardBacks")); empty.Save(path);
        using (var profile = new LocalProfile(path)) check(profile.FavoriteCardBacks.SequenceEqual(new[] { 0 }), "empty favourites normalize to Classic on load");
        var unsorted = new XDocument(saved); unsorted.Root!.Add(new XElement("FavoriteCardBacks", new XElement("int", 122), new XElement("int", 0))); unsorted.Save(path);
        using (var profile = new LocalProfile(path)) {
            var snapshot = (LocalProfileData)typeof(LocalProfile).GetMethod("Snapshot", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(profile, null)!;
            check(snapshot.FavoriteCardBacks.SequenceEqual(new[] { 0, 122 }), "load stores favourites sorted ascending");
        }
        foreach (int[] invalid in new[] { new[] { 0, 0 }, new[] { 9876 } }) {
            var doc = new XDocument(saved); doc.Root!.Add(new XElement("FavoriteCardBacks", invalid.Select(id => new XElement("int", id)))); doc.Save(path);
            reject(() => { using var profile = new LocalProfile(path); }, "invalid favourite list rejected on load");
        }
        // Validation also protects commits before they can write an empty or null ledger.
        var validate = typeof(LocalProfile).GetMethod("ValidatePurchases", BindingFlags.Static | BindingFlags.NonPublic)!;
        foreach (var invalid in new List<int>[] { new(), null! }) {
            LocalProfileData data;
            using (var stream = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(stream)!;
            data.FavoriteCardBacks = invalid;
            bool refused = false;
            try { validate.Invoke(null, new object[] { data, false, false }); }
            catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { refused = true; }
            check(refused, "validation rejects empty or null favourite list");
        }
        Console.WriteLine("PASS multiple favourite backs: XML, legacy, atomic add/remove, ownership, validation, seeded choice and detached snapshots");
    }
}
