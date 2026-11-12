using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using E = SabberStoneCore.Kettle.IPowerHistoryEntry;

// NPC opponent pets (PROPOSED, docs/design/npc-pets.md): shop-backed seat resolution and the native transport.
// The pure resolver has its own suite in tests/npc-world.
internal static partial class Program
{
    private static void NpcPetSeatTests()
    {
        using var fixture = new CosmeticsFixture();
        var shop = LocalShop.Default;
        var families = NpcPetSeats.Families(shop);
        Check(families.Select(f => f.NativeId).OrderBy(i => i).SequenceEqual(new[] { 3, 4, 6, 7, 8, 9, 10, 12, 15, 16 }), "all ten admitted families listed");
        foreach (var family in families)
            Check(family.Forms.SequenceEqual(shop.FormsForPet(family.Family)) && family.NativeId == shop.PetNativeId(family.Family), "forms and native id come from the catalog: " + family.Family);
        // A family still marked verify is never shown.
        string text;
        using (var source = typeof(LocalShop).Assembly.GetManifestResourceStream("SoloStone.Shop.catalog-v1.json"))
        using (var reader = new StreamReader(source)) text = reader.ReadToEnd();
        string flagged = text.Replace("\"verify\": true", "\"verify\": false");
        int at = flagged.IndexOf("\"Id\": \"pet-PET_3_1\"", StringComparison.Ordinal);
        Check(at > 0, "pet-PET_3_1 row found");
        int verify = flagged.IndexOf("\"verify\": false", at, StringComparison.Ordinal);
        flagged = flagged.Substring(0, verify) + "\"verify\": true" + flagged.Substring(verify + "\"verify\": false".Length);
        var original = LocalShop.Default;
        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(flagged)))
        {
            var unverified = LocalShop.Load(stream, () => original.Library);
            Check(NpcPetSeats.Families(unverified).Count == 9 && NpcPetSeats.Families(unverified).All(f => f.Family != "PET_3_1"), "verify:true family skipped");
        }

        NpcPetSeatWorldTests();
        // Records: no NPC id (legacy and automation opponents) or no displayed rating shows nothing.
        Check(NpcPetSeats.For(null, shop) == null, "no record, no pet");
        Check(NpcPetSeats.For(new LocalMatchRecord { Id = "m", Opponent = "deck", NpcRating = 1500 }, shop) == null, "no NpcId, no pet");
        Check(NpcPetSeats.For(new LocalMatchRecord { Id = "m", Opponent = "deck", NpcId = "npc.g.1" }, shop) == null, "no rating, no pet");
        Check(NpcPetSeats.For(new LocalMatchRecord { Id = "m", Opponent = "deck", NpcId = "npc.g.1", NpcRating = double.NaN }, shop) == null, "bad rating, no pet");
        // With the world absent the displayed rating stands in for the home rating; the card is a catalog form.
        int shown = 0;
        for (int i = 0; i < 400; i++)
        {
            var record = new LocalMatchRecord { Id = "m", Opponent = "deck-x", NpcId = "npc.g.seat" + i, NpcDeckId = "deck-x", NpcRating = LadderRules.RatingForIndex(LadderRules.LegendIndex) };
            var pet = NpcPetSeats.For(record, shop);
            if (pet == null) continue;
            shown++;
            Check(shop.PetFamily(pet.CardId) == pet.Family && shop.FormsForPet(pet.Family)[pet.Form - 1] == pet.CardId && pet.Form >= 2, "seat pet is a catalog form: " + pet.CardId);
            Check(NpcPetSeats.For(record, shop).CardId == pet.CardId, "same record, same pet");
        }
        Check(shown > 140 && shown < 260, "about half of Legend NPCs own a pet (owner ruling 50%): " + shown);
    }
    // The world-identity path: presence follows the HOME band (identity), never the drifting displayed rating, notables included; the form follows the rank the player sees, which in Casual is the player's ladder rank, never the hidden Casual rating.
    private static void NpcPetSeatWorldTests()
    {
        string Data(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "npc", name));
        NpcWorldJson.Parser = EmbeddedJson.Parse;
        var world = NpcWorld.Generate(new NpcWorldCatalogs(NpcDeckLibrary.Parse(Data("decks-v1.json")), SkillCatalog.Parse(Data("skills-v1.json")),
            PersonalityCatalog.Parse(Data("personalities-v1.json")), NpcNotables.Parse(Data("notables-v1.json")),
            NpcNameTables.Parse(Data("names-v1.json")), NpcCosmeticPool.Parse(Data("cosmetics-v1.json"))));
        var shop = LocalShop.Default;
        var families = NpcPetSeats.Families(shop);
        Check(NpcWorldRuntime.TryInstallClient(() => world, deck => null, null), "world installs for pet seats");
        try
        {
            LocalMatchRecord Record(NpcIdentity npc, double rating, string mode = null) => new LocalMatchRecord { Id = "m", Opponent = npc.DeckIds[0], NpcId = npc.Id,
                NpcDeckId = npc.DeckIds[0], NpcRating = rating, Mode = mode };
            int checkedPresence = 0, checkedNotable = 0, ownersSeen = 0;
            foreach (var npc in world.All.Where(n => n.Id.StartsWith("npc.g.", StringComparison.Ordinal)).Take(120))
            {
                int home = LadderRules.IndexForRating(npc.HomeRating);
                bool expected = NpcPets.Resolve(new NpcPetSeat(npc.Id, npc.DeckIds[0], home, home), families) != null;
                var seen = new List<NpcPet>();
                foreach (int shown in new[] { 0, 30, 60, 90, 120, LadderRules.LegendIndex })
                {
                    var pet = NpcPetSeats.For(Record(npc, LadderRules.RatingForIndex(shown)), shop);
                    Check((pet != null) == expected, "presence follows the identity's home band, not the displayed rank: " + npc.Id + " at " + shown);
                    if (pet != null) seen.Add(pet);
                }
                if (seen.Count > 1) Check(seen.Select(p => p.Family).Distinct().Count() == 1 && seen.Zip(seen.Skip(1), (a, b) => b.Form >= a.Form).All(ok => ok), "same family, never a lower form at a higher rank: " + npc.Id);
                checkedPresence++; if (expected) ownersSeen++;
            }
            Check(checkedPresence == 120 && ownersSeen >= 3 && ownersSeen < 80, "the sample mixes owners and non-owners: " + ownersSeen);
            // Notables follow the same share table (owner ruling 2026-10-28): some own a pet, some do not.
            int notableOwners = 0;
            foreach (var notable in world.All.Where(n => n.Notable).Take(40))
            {
                int home = LadderRules.IndexForRating(notable.HomeRating);
                bool expected = NpcPets.Resolve(new NpcPetSeat(notable.Id, notable.DeckIds[0], home, home), families) != null;
                foreach (int shown in new[] { 0, LadderRules.LegendIndex })
                    Check((NpcPetSeats.For(Record(notable, LadderRules.RatingForIndex(shown)), shop) != null) == expected, "a notable follows the shared presence rule: " + notable.Id);
                checkedNotable++; if (expected) notableOwners++;
            }
            Check(checkedNotable > 0 && notableOwners > 0 && notableOwners < checkedNotable, "notables are not all owners: " + notableOwners + "/" + checkedNotable);
            // Casual: the form follows the player's visible rank whatever the hidden Casual rating says; Ranked is unchanged.
            var owners = world.All.Where(n => NpcPetSeats.For(Record(n, LadderRules.RatingForIndex(105)), shop) != null).Take(40).ToList();
            Check(owners.Count == 40, "the world has enough pet owners");
            var casual = owners[0];
            var ranked = NpcPetSeats.For(Record(casual, LadderRules.RatingForIndex(105)), shop);
            foreach (double hidden in new[] { 900.0, LadderRules.RatingForIndex(20), LadderRules.RatingForIndex(130), 3000.0 })
            {
                var pet = NpcPetSeats.For(Record(casual, hidden, CasualRules.Mode), shop, playerStarIndex: 105);
                Check(pet != null && ranked != null && pet.CardId == ranked.CardId && pet.Family == ranked.Family,
                    "Casual form ignores the hidden Casual rating and follows the player's visible rank (105): " + hidden);
            }
            bool formsDiffer = false;
            foreach (var npc in owners)
                formsDiffer |= NpcPetSeats.For(Record(npc, 1000, CasualRules.Mode), shop, 0)?.Form != NpcPetSeats.For(Record(npc, 1000, CasualRules.Mode), shop, LadderRules.LegendIndex)?.Form;
            Check(formsDiffer, "Casual forms do move with the player's rank");
            var rankedLow = NpcPetSeats.For(Record(casual, LadderRules.RatingForIndex(0)), shop);
            var rankedHigh = NpcPetSeats.For(Record(casual, LadderRules.RatingForIndex(LadderRules.LegendIndex)), shop);
            Check(rankedLow != null && rankedHigh != null && rankedHigh.Form >= rankedLow.Form, "Ranked still follows the NPC's own medal");
            Check(NpcPetSeats.For(Record(casual, 1000, CasualRules.Mode), shop, 105).Family == NpcPetSeats.For(Record(casual, 2800, null), shop, 0).Family,
                "the shown family is a trait of the NPC and deck in every mode");
        }
        finally { NpcWorldRuntime.Uninstall(); NpcWorldHooks.Reset(); LocalDecks.ExternalDecks = null; PlaytestQueue.WorldProfileResolver = null; }
    }
}
