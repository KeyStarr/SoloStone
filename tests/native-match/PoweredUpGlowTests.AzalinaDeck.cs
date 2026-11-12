using System;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using SabberStoneCore.Model;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("native-azalina-deck", ProofKind.Native)]
    private static void AzalinaDeckSave()
    {
        var ids = LocalDecks.Get("test-standard-azalina-priest").CardIds.ToArray();
        var nineteen = ids.Skip(1).ToArray(); // Remove one filler copy; retain Azalina and the complete Fabled bundle.
        LocalDecks.ValidateNativeSave(ids, null, true);
        LocalDecks.ValidateNativeSave(nineteen, null);
        foreach (var invalid in new[] { nineteen, ids.Concat(Enumerable.Repeat("CORE_CS2_004", 1)).ToArray(),
            ids.Concat(Enumerable.Repeat("CORE_CS2_004", 10)).ToArray() })
        {
            bool rejected = false;
            try { LocalDecks.ValidateNativeSave(invalid, null, true); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "native complete Azalina rejects " + invalid.Length);
            // Incomplete drafts may exceed the twenty-card rule up to thirty, like retail "too many cards" decks.
            if (invalid.Length > 20) LocalDecks.ValidateNativeSave(invalid, null);
        }
        bool overThirty = false;
        try { LocalDecks.ValidateNativeSave(ids.Concat(Enumerable.Repeat("CORE_CS2_004", 11)).ToArray(), null); }
        catch (InvalidOperationException) { overThirty = true; }
        Check(overThirty, "native draft Azalina rejects 31");
        string dir = Path.Combine(Path.GetTempPath(), "azalina-native-" + Guid.NewGuid());
        string path = Path.Combine(dir, "profile.xml");
        try
        {
            using (var profile = new LocalProfile(path)) profile.SelectCardBack(0);
            var serializer = new XmlSerializer(typeof(LocalProfileData));
            LocalProfileData data;
            using (var file = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(file);
            foreach (var group in ids.Where(id => Cards.FromId(id).Collectible).GroupBy(id => id))
            {
                var row = data.Collection.SingleOrDefault(card => card.Id == group.Key);
                if (row == null) { row = new LocalCardCount { Id = group.Key }; data.Collection.Add(row); }
                row.Count = Math.Max(row.Count, group.Count()); row.Acquired = Math.Max(row.Acquired, group.Count());
            }
            using (var file = File.Create(path)) serializer.Serialize(file, data);
            long deckId;
            using (var profile = new LocalProfile(path))
            {
                var draft = profile.CreateDraft("Azalina twenty", "HERO_09"); deckId = draft.Id;
                var prior = B18Deck(draft, Array.Empty<string>());
                var incomplete = NativeUIProbe.CommitLocalDraft(profile, B18Deck(draft, nineteen), prior);
                Check(!profile.IsDeckComplete(deckId), "nineteen saved as incomplete draft");
                var saved = NativeUIProbe.CommitLocalDraft(profile, B18Deck(draft, ids), B18Deck(incomplete, incomplete.Cards));
                var owned = profile.Collection.ToDictionary(row => row.Key, row => row.Value);
                NativeUIProjection.ValidateCustomSelection(deckId, saved.Hero, ids, saved.Hero, ids, owned);
                Check(NativeUIProjection.ValidateDraftProjection(deckId, saved.Name, saved.Hero, ids, saved.Name, saved.Hero, ids, owned),
                    "twenty-card selection and native projection");
                var selected = LocalProfile.LedgerDeck(saved.Name, saved.Hero, saved.Cards, saved.Sideboards, false);
                Check(profile.IsDeckComplete(deckId) && selected.Build().Count == 20, "saved twenty playable and builds");
                NativeUIProjection.ValidateStart(profile, "custom:" + deckId, selected, true);
                var match = new NativeMatch((id, message) => {}, selected, "mage", 19); match.Start();
                Check(match.Game.Player1.DeckCards.Count == 20 && match.Game.Player1.Hero.Health == 40,
                    "native match preserves submitted twenty and Azalina Health");
            }
            using (var profile = new LocalProfile(path))
            {
                var saved = profile.GetDraft(deckId);
                Check(saved.Cards.SequenceEqual(ids) && profile.IsDeckComplete(deckId)
                    && LocalProfile.PlayableDeck(saved.Name, saved.Hero, saved.Cards, saved.Sideboards).Build().Count == 20,
                    "twenty-card save survives restart");
                Check(profile.VisibleDeckSummaries().Single().DeckSize == 20, "diagnostic summary uses actual limit");
            }
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        Console.WriteLine("PASS native Azalina save, selection, projection, match and restart");
    }
}
