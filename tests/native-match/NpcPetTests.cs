using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using NativeHistory = PegasusGame.PowerHistoryData;
using E = SabberStoneCore.Kettle.IPowerHistoryEntry;

// NPC opponent pets (PROPOSED, docs/design/npc-pets.md): shop-backed seat resolution and the native transport.
// The pure resolver has its own suite in tests/npc-world.
internal static partial class Program
{
    private static void NpcPets_()
    {
        NpcPetSeatTests();
        NpcPetTransport();
        Console.WriteLine("PASS npc pets transport: seat resolution, both pet entities, player links, rewind, opposing reactions, profile isolation");
    }

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

    private static PegasusGame.PowerHistoryEntity PetEntity(IEnumerable<NativeHistory> history, string card) =>
        history.Single(e => e.HasFullEntity && e.FullEntity.Name == card).FullEntity;
    private static int PetTag(PegasusGame.PowerHistoryEntity entity, int tag) => entity.Tags.Single(t => t.Name == tag).Value;
    private static byte[] Bytes(PegasusGame.PowerHistoryEntity entity)
    {
        using var stream = new MemoryStream(); entity.Serialize(stream); return stream.ToArray();
    }

    private static void NpcPetTransport()
    {
        using var fixture = new CosmeticsFixture();
        var previousProfile = NativeMatch.CosmeticsProfile;
        var previousVariant = NativeMatch.NativePetVariant;
        string root = Path.Combine(Path.GetTempPath(), "npc-pets-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try
        {
            string path = Path.Combine(root, "profile.xml");
            using (var fresh = new LocalProfile(path)) fresh.SelectCardBack(0);
            var serializer = new XmlSerializer(typeof(LocalProfileData)); LocalProfileData data;
            using (var file = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(file);
            data.GoldBalance = 5000;
            using (var file = File.Create(path)) serializer.Serialize(file, data);
            using var profile = new LocalProfile(path);
            profile.Purchase("pet-PET_3_1", 1, 5000, "npc-pets");
            profile.SelectPet("PET_3_1");
            profile.Begin("mage", "warrior", 1337, "npc.g.pet", "warrior", 1500);
            NativeMatch.CosmeticsProfile = () => profile;
            NativeMatch.NativePetVariant = card => card == "PET_3_1" ? 5 : card == "PET_4_3" ? 14 : 0;
            Check(profile.Pending.Pet == "PET_3_1", "human pet frozen into the pending record");

            // With and without the NPC pet, same seed: the human pet's wire bytes and id never change.
            var baseline = new Capture();
            var plain = new NativeMatch(baseline.Send, "mage", "warrior", 1337); plain.Start();
            // Start freezes the resolved human coin; the NPC pet must not change that baseline or any other saved bytes.
            byte[] profileBefore = File.ReadAllBytes(path);
            var pendingBefore = Serialize(profile.Pending);
            var withNpc = new Capture();
            var match = new NativeMatch(withNpc.Send, "mage", "warrior", 1337) { OpponentPetCard = "PET_4_3" }; match.Start();
            var plainInitial = NativeHistory(baseline); var initial = NativeHistory(withNpc);

            var humanPlain = PetEntity(plainInitial, "PET_3_1"); var human = PetEntity(initial, "PET_3_1");
            Check(Bytes(humanPlain).SequenceEqual(Bytes(human)), "human pet entity bytes unchanged by the NPC pet");
            Check(plainInitial.Count(e => e.HasFullEntity && e.FullEntity.Name?.StartsWith("PET_") == true) == 1, "no NPC pet without OpponentPetCard");
            var npc = PetEntity(initial, "PET_4_3");
            Check(initial.Count(e => e.HasFullEntity && e.FullEntity.Name?.StartsWith("PET_") == true) == 2 && npc.Entity != human.Entity, "two distinct pet entities");
            Check(npc.Entity == human.Entity - 1, "NPC pet id allocated right after the human pet");
            Check(PetTag(npc, 202) == 45 && PetTag(npc, 49) == 9 && PetTag(npc, 50) == 2 && PetTag(npc, 4079) == 4 && PetTag(npc, 4037) == 14
                && PetTag(npc, 53) == npc.Entity, "NPC pet: cosmetic zone, controller 2, family, variant, entity id");
            Check(PetTag(human, 50) == 1 && PetTag(human, 4037) == 5, "human pet keeps controller 1 and its variant");
            // Treats and XP are friendly-only: the human pet alone carries PET_TREATS_GENERATED; the NPC pet has no treat or counter tags.
            Check(npc.Tags.Select(t => t.Name).OrderBy(i => i).SequenceEqual(human.Tags.Where(t => t.Name != 4281).Select(t => t.Name).OrderBy(i => i)), "NPC pet carries the human pet's tags minus the friendly treat counter");
            Check(!npc.Tags.Any(t => t.Name == 4280 || t.Name == 4281 || t.Name >= 4076 && t.Name <= 4078), "NPC pet has no treat or XP counter tags");
            var players = initial.Single(e => e.HasCreateGame).CreateGame.Players;
            var plainPlayers = plainInitial.Single(e => e.HasCreateGame).CreateGame.Players;
            Check(PetTag2(players.Single(p => p.Id == 1), 4017) == human.Entity && PetTag2(players.Single(p => p.Id == 1), 4037) == 5, "human player link unchanged");
            Check(PetTag2(players.Single(p => p.Id == 2), 4017) == npc.Entity && PetTag2(players.Single(p => p.Id == 2), 4037) == 14, "NPC player links its pet and corner variant");
            Check(!plainPlayers.Single(p => p.Id == 2).Entity.Tags.Any(t => t.Name == 4017 || t.Name == 4037), "no NPC links without an NPC pet");
            Check(players.Single(p => p.Id == 1).Entity.Tags.Select(t => (t.Name, t.Value)).SequenceEqual(plainPlayers.Single(p => p.Id == 1).Entity.Tags.Select(t => (t.Name, t.Value))),
                "human player entity unchanged");
            int humanIndex = initial.FindIndex(e => e.HasFullEntity && e.FullEntity.Name == "PET_3_1");
            Check(initial.FindIndex(e => e.HasFullEntity && e.FullEntity.Name == "PET_4_3") == humanIndex + 1, "NPC pet follows the human pet in the packet");

            // Opposing reactions: each pet hears events from its owner's side only, never the other pet's.
            match.SendChoices(withNpc.Last<PegasusGame.EntityChoices>(17).Id, new List<int>());
            var game = match.Game;
            int cursor = withNpc.Packets.Count;
            game.PowerHistory.Last.Clear();
            game.Player1.Hero.TakeDamage(game.Player2.Hero, 6);
            game.Player2.Hero.TakeDamage(game.Player1.Hero, 5);
            game.Player2.Hero.TakeHeal(game.Player2.Hero, 4);
            FlushHistory(match);
            var history = NativeHistory(withNpc, cursor);
            int[] For(int pet) => history.Where(e => e.HasTagChange && e.TagChange.Tag == 4038 && e.TagChange.Entity == pet && e.TagChange.Value != 0).Select(e => e.TagChange.Value).ToArray();
            Check(For(human.Entity).SequenceEqual(new[] { 4, 1, 25 }), "human pet: own damage friendly, NPC damage and heal opposing: " + string.Join(",", For(human.Entity)));
            Check(For(npc.Entity).SequenceEqual(new[] { 2, 3, 27 }), "NPC pet mirrors from its side: " + string.Join(",", For(npc.Entity)));
            Check(history.Where(e => e.HasTagChange && e.TagChange.Tag == 4038).All(e => e.TagChange.Entity == human.Entity || e.TagChange.Entity == npc.Entity), "reactions only target pets");
            int firstHuman = history.FindIndex(e => e.HasTagChange && e.TagChange.Tag == 4038 && e.TagChange.Entity == human.Entity);
            Check(firstHuman >= 0 && history[firstHuman + 2].TagChange?.Entity == npc.Entity && history[firstHuman + 2].TagChange.Tag == 4038, "NPC reaction merges right after the human pet's at the same entry");

            // No human pet (no cosmetics at all): only the NPC pet exists and reacts.
            NativeMatch.CosmeticsProfile = null;
            var solo = new Capture();
            var soloMatch = new NativeMatch(solo.Send, "mage", "warrior", 1337) { OpponentPetCard = "PET_4_3" }; soloMatch.Start();
            var soloInitial = NativeHistory(solo);
            Check(soloInitial.Count(e => e.HasFullEntity && e.FullEntity.Name?.StartsWith("PET_") == true) == 1
                && PetEntity(soloInitial, "PET_4_3") != null, "an NPC pet needs no human pet");
            var soloPlayers = soloInitial.Single(e => e.HasCreateGame).CreateGame.Players;
            Check(!soloPlayers.Single(p => p.Id == 1).Entity.Tags.Any(t => t.Name == 4017 || t.Name == 4037)
                && PetTag2(soloPlayers.Single(p => p.Id == 2), 4017) == PetEntity(soloInitial, "PET_4_3").Entity, "only the NPC player links a pet");
            soloMatch.SendChoices(solo.Last<PegasusGame.EntityChoices>(17).Id, new List<int>());
            solo.Packets.RemoveAll(packet => true); soloMatch.Game.PowerHistory.Last.Clear();
            soloMatch.Game.Player1.Hero.TakeDamage(soloMatch.Game.Player2.Hero, 6); FlushHistory(soloMatch);
            Check(NativeHistory(solo).Where(e => e.HasTagChange && e.TagChange.Tag == 4038 && e.TagChange.Value != 0)
                .Select(e => (e.TagChange.Entity, e.TagChange.Value)).SequenceEqual(new[] { (PetEntity(soloInitial, "PET_4_3").Entity, 2) }), "NPC pet reacts alone");
            NativeMatch.CosmeticsProfile = () => profile;

            // Rewind reinjects both pets with the same ids, exactly once each. (The cleanup exclusion for the pet ids is
            // defensive: pets never enter the tracked client state, so no test can observe it; none is claimed.)
            var append = typeof(NativeMatch).GetMethod("AppendRewindReset", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var entries = new List<E> { PowerHistoryBuilder.BlockStart(BlockType.GAME_RESET, game.Player1.Hero.Id, "", -1, 0), PowerHistoryBuilder.ResetGame(game) };
                entries.AddRange(game.IdEntityDic.Values.OrderBy(e => e.Id).Select(e => PowerHistoryBuilder.FullEntity(e, reset: true)));
                entries.Add(PowerHistoryBuilder.BlockEnd());
                var reset = new PegasusGame.PowerHistory(); append.Invoke(match, new object[] { reset, entries, 0 });
                Check(reset.List.Count(e => e.HasFullEntity && e.FullEntity.Name == "PET_3_1" && e.FullEntity.Entity == human.Entity) == 1
                    && reset.List.Count(e => e.HasFullEntity && e.FullEntity.Name == "PET_4_3" && e.FullEntity.Entity == npc.Entity) == 1, "rewind reinjects each pet once");
                var resetPlayers = reset.List.Single(e => e.HasResetGame).ResetGame.CreateGame.Players;
                Check(PetTag2(resetPlayers.Single(p => p.Id == 1), 4017) == human.Entity && PetTag2(resetPlayers.Single(p => p.Id == 2), 4017) == npc.Entity
                    && PetTag2(resetPlayers.Single(p => p.Id == 2), 4037) == 14, "reset players keep both pet links");
            }

            // Reaction state survives a reset the same way the human pet's does: the same hit reacts once again.
            game.PowerHistory.Last.Clear(); game.Player1.Hero.TakeDamage(game.Player2.Hero, 6);
            cursor = withNpc.Packets.Count; FlushHistory(match);
            Check(NativeHistory(withNpc, cursor).Where(e => e.HasTagChange && e.TagChange.Tag == 4038 && e.TagChange.Value != 0).Select(e => (e.TagChange.Entity, e.TagChange.Value))
                .SequenceEqual(new[] { (human.Entity, 4), (npc.Entity, 2) }), "both pets react once after rewind");

            // A missing or invalid native variant only hides the NPC pet; the match still starts.
            NativeMatch.NativePetVariant = card => card == "PET_3_1" ? 5 : 0;
            var hidden = new Capture();
            new NativeMatch(hidden.Send, "mage", "warrior", 1337) { OpponentPetCard = "PET_4_3" }.Start();
            var hiddenInitial = NativeHistory(hidden);
            Check(hiddenInitial.Count(e => e.HasFullEntity && e.FullEntity.Name?.StartsWith("PET_") == true) == 1
                && !hiddenInitial.Single(e => e.HasCreateGame).CreateGame.Players.Single(p => p.Id == 2).Entity.Tags.Any(t => t.Name == 4017 || t.Name == 4037), "no native variant, no NPC pet");
            NativeMatch.NativePetVariant = card => card == "PET_3_1" ? 5 : throw new InvalidOperationException("mapping offline");
            var thrown = new Capture();
            new NativeMatch(thrown.Send, "mage", "warrior", 1337) { OpponentPetCard = "PET_4_3" }.Start();
            Check(NativeHistory(thrown).Count(e => e.HasFullEntity && e.FullEntity.Name?.StartsWith("PET_") == true) == 1, "a failing native mapping also only hides the NPC pet");

            // A card the shop cannot resolve to a family also only hides the NPC pet (resolved in Start, not while building packets).
            NativeMatch.NativePetVariant = card => card == "PET_3_1" ? 5 : 77;
            var unknownFamily = new Capture();
            new NativeMatch(unknownFamily.Send, "mage", "warrior", 1337) { OpponentPetCard = "NOT_A_PET" }.Start();
            Check(NativeHistory(unknownFamily).Count(e => e.HasFullEntity && e.FullEntity.Name?.StartsWith("PET_") == true) == 1, "an unresolvable family only hides the NPC pet");
            NativeMatch.NativePetVariant = card => card == "PET_3_1" ? 5 : card == "PET_4_3" ? 14 : 0;

            // Isolation: profile bytes and the pending record never see the NPC pet.
            withNpc.SerializeAll();
            Check(File.ReadAllBytes(path).SequenceEqual(profileBefore), "profile bytes unchanged by a match with an NPC pet");
            Check(Serialize(profile.Pending) == pendingBefore, "pending record unchanged: Pet, PetXpBefore, rules");
            Check(profile.Pending.Pet != "PET_4_3" && profile.OwnedPets.Single() == "PET_3_1" && !profile.PetProgress.Any(p => p.Pet == "PET_4_3"), "NPC family never owned or progressed");
            Check(profile.SelectedPet != "PET_4_3" && profile.CanUsePetForm("PET_4_3") == false, "NPC pet never selectable");
            // ...and settlement: the receipt knows only the human pet, and nothing about the NPC's family appears anywhere.
            var settled = profile.Settle(profile.Pending.Id, "Won", 8, false);
            Check(settled.Pet == "PET_3_1" && settled.PetXpBefore == 0 && !Serialize(settled).Contains("PET_4_3")
                && profile.OwnedPets.Single() == "PET_3_1" && profile.PetProgress.Single().Pet == "PET_3_1" && !profile.History.Any(m => Serialize(m).Contains("PET_4_")),
                "settlement after a match with an NPC pet records only the human pet");
        }
        finally { NativeMatch.CosmeticsProfile = previousProfile; NativeMatch.NativePetVariant = previousVariant; Directory.Delete(root, true); }
    }

    private static int PetTag2(PegasusGame.Player player, int tag) => player.Entity.Tags.Single(t => t.Name == tag).Value;
    private static string Serialize(LocalMatchRecord record)
    {
        using var writer = new StringWriter(); new XmlSerializer(typeof(LocalMatchRecord)).Serialize(writer, record); return writer.ToString();
    }
}
