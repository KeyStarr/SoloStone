using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

internal static partial class PoweredUpGlowTests
{
    private static void B18RecipeNativeSave()
    {
        string dir = Path.Combine(Path.GetTempPath(), "recipe-native-save-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "profile.xml");
        var expected = new Dictionary<long, LocalCustomDeck>();
        try
        {
            using (var profile = new LocalProfile(path))
            foreach (var recipe in LocalShop.Default.Library.Recipes.OrderBy(recipe => recipe.Id == "pure-paladin" ? 0 : 1))
            {
                var built = LocalShop.Default.Library.BuildDeck(recipe, _ => 0).Deck;
                var draft = profile.CreateDraft(recipe.Name, recipe.Hero);
                var collection = profile.Collection.OrderBy(row => row.Key).ToArray();
                var working = B18Deck(draft, built.CardIds);
                working.SetRuneOrder(NativeDeckRunes.Order(draft.Hero, built.CardIds));
                var rows = built.Sideboards.Select(row => new LocalSideboard { Source = row.Key, Cards = row.Value.ToList() }).ToArray();
                B18SideboardFixture(working, rows);
                var prior = B18Deck(draft, Array.Empty<string>());
                prior.SetRuneOrder(working.GetRuneOrder());
                var saved = NativeUIProbe.CommitLocalDraft(profile, working, prior);
                Check(saved.Cards.OrderBy(id => id).SequenceEqual(built.CardIds.OrderBy(id => id))
                    && saved.Sideboards.Count == built.Sideboards.Count, "every shipped recipe native save: " + recipe.Id);
                Check(NativeUIProbe.ReadSideboards(working).Length == built.Sideboards.Count, "Fabled companions stay main slots, sideboard selections preserved: " + recipe.Id);
                // Exit/save with missing and owned native slots; neither state grants collection copies.
                foreach (bool owned in new[] { false, true })
                {
                    foreach (var board in working.GetAllSideboards().Values)
                        foreach (var slot in board.GetSlots()) slot.Owned = owned;
                    NativeUIProbe.CommitLocalDraft(profile, working, working);
                    Check(NativeUIProbe.SideboardSignature(profile.GetDraft(saved.Id).Sideboards)
                        == NativeUIProbe.SideboardSignature(rows), "owned/missing selections survive exit/save: " + recipe.Id + "/" + owned);
                }
                if (recipe.Id == "pure-paladin")
                {
                    Check(built.CardIds.Contains("TIME_009t1") && built.CardIds.Contains("TIME_009t2")
                        && rows.Single().Source == "JAIL_397" && rows.Single().Cards.SequenceEqual(new[] { "JAIL_328" }),
                        "Gelbin bundle and single Scarlet Bruiser selection pinned");
                    var emptySide = B18Deck(draft, built.CardIds);
                    B18SideboardFixture(emptySide, new[] { new LocalSideboard { Source = "JAIL_397" } });
                    NativeUIProbe.CommitLocalDraft(profile, emptySide, working);
                    Check(!profile.IsDeckComplete(saved.Id) && profile.GetDraft(saved.Id).Sideboards.Single().Cards.Count == 0,
                        "incomplete Beatrix draft can save/exit without losing main cards");
                    NativeUIProbe.CommitLocalDraft(profile, working, emptySide);
                }
                Check(profile.Collection.OrderBy(row => row.Key).SequenceEqual(collection), "recipe save never grants missing copies: " + recipe.Id);
                expected[saved.Id] = saved;
                // Keep the library small while exercising every shipped recipe.
                if (expected.Count > 1) { var old = expected.Keys.First(); profile.DeleteDraft(old); expected.Remove(old); }
            }
            using (var profile = new LocalProfile(path))
            foreach (var row in expected)
                Check(profile.GetDraft(row.Key).Cards.SequenceEqual(row.Value.Cards)
                    && NativeUIProbe.SideboardSignature(profile.GetDraft(row.Key).Sideboards) == NativeUIProbe.SideboardSignature(row.Value.Sideboards),
                    "recipe native save survives restart");
        }
        finally { Directory.Delete(dir, true); }
    }
}
