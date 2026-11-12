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
        NpcPetTransport();
        Console.WriteLine("PASS npc pets transport: both pet entities, player links, rewind, opposing reactions, profile isolation");
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
