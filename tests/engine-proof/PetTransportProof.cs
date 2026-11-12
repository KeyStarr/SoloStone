using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using MiniJSON;
using PowerHistory = PegasusGame.PowerHistory;
using SabberStoneCore.Kettle;

internal static class PetTransportProof
{
    [GroupProof("pet-transport")]
    internal static void Run()
    {
        var previousShop = LocalShop.TestOverride;
        var previousProfile = NativeMatch.CosmeticsProfile;
        var previousVariant = NativeMatch.NativePetVariant;
        string root = Path.Combine(Path.GetTempPath(), "hs-pet-transport-" + Guid.NewGuid()); Directory.CreateDirectory(root);
        try {
            var original = LocalShop.Default;
            using (var stream = typeof(LocalShop).Assembly.GetManifestResourceStream("SoloStone.Shop.catalog-v1.json"))
            using (var reader = new StreamReader(stream))
            using (var admitted = new MemoryStream(Encoding.UTF8.GetBytes(reader.ReadToEnd().Replace("\"verify\": true", "\"verify\": false"))))
                LocalShop.TestOverride = LocalShop.Load(admitted, () => original.Library);
            string path = Path.Combine(root, "profile.xml");
            using (var p = new LocalProfile(path)) p.SelectCardBack(0);
            var serializer = new XmlSerializer(typeof(LocalProfileData)); LocalProfileData funded;
            using (var source = File.OpenRead(path)) funded = (LocalProfileData)serializer.Deserialize(source);
            funded.GoldBalance = 5000; using (var target = File.Create(path)) serializer.Serialize(target, funded);
            using var profile = new LocalProfile(path);
            profile.Purchase("pet-PET_3_1", 1, 5000, "pet-transport");
            var pending = profile.Begin("mage", "priest", 71); profile.SelectPet(null);
            NativeMatch.CosmeticsProfile = () => profile;
            // Exact pinned PetVariant ID5, independently exported from PET/Card DBF IDs.
            NativeMatch.NativePetVariant = card => card == "PET_3_1" ? 5 : throw new Exception("Unexpected form");
            var packets = new List<PowerHistory>();
            var match = new NativeMatch((id, packet) => { if (id == 19) packets.Add((PowerHistory)packet); }, "mage", "priest", 71);
            match.Start();
            var initial = packets.SelectMany(packet => packet.List).ToArray();
            var pet = initial.Single(entry => entry.HasFullEntity && entry.FullEntity.Name == "PET_3_1").FullEntity;
            int Tag(int name) => pet.Tags.Single(tag => tag.Name == name).Value;
            Core2026Proof.Check(Tag(202) == 45 && Tag(49) == 9 && Tag(50) == 1 && Tag(4079) == 3 && Tag(4037) == 5,
                "pet packet has exact cosmetic zone, controller, family and native variant identity");
            Core2026Proof.Check(Tag(4281) == 1 && !pet.Tags.Any(tag => tag.Name == 4280 || tag.Name >= 4076 && tag.Name <= 4078),
                "retail pet starts with one generated treat, no feeds and no XP counters");
            var player = initial.Single(entry => entry.HasCreateGame).CreateGame.Players.Single(player => player.Id == 1);
            Core2026Proof.Check(player.Entity.Tags.Single(tag => tag.Name == 4017).Value == pet.Entity
                && player.Entity.Tags.Single(tag => tag.Name == 4037).Value == 5,
                "player links to the frozen pet entity and native corner variant even after selection changed to None");
            Core2026Proof.Check(!initial.Single(entry => entry.HasCreateGame).CreateGame.Players.Single(player => player.Id == 2)
                .Entity.Tags.Any(tag => tag.Name == 4017 || tag.Name == 4037), "opponent has no local pet linkage or corner variant");
            var append = typeof(NativeMatch).GetMethod("AppendRewindReset", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int attempt = 0; attempt < 2; attempt++) {
                var entries = new List<IPowerHistoryEntry> { PowerHistoryBuilder.BlockStart(SabberStoneCore.Enums.BlockType.GAME_RESET, match.Game.Player1.Hero.Id, "", -1, 0),
                    PowerHistoryBuilder.ResetGame(match.Game) };
                entries.AddRange(match.Game.IdEntityDic.Values.OrderBy(entity => entity.Id).Select(entity => PowerHistoryBuilder.FullEntity(entity, reset: true)));
                entries.Add(PowerHistoryBuilder.BlockEnd());
                var reset = new PowerHistory(); append.Invoke(match, new object[] { reset, entries, 0 });
                var pets = reset.List.Where(entry => entry.HasFullEntity && entry.FullEntity.Name == "PET_3_1").ToArray();
                var resetPlayer = reset.List.Single(entry => entry.HasResetGame).ResetGame.CreateGame.Players.Single(player => player.Id == 1);
                Core2026Proof.Check(pets.Length == 1 && pets[0].FullEntity.Entity == pet.Entity && pets[0].FullEntity.Tags.Single(tag => tag.Name == 4281).Value == 1
                    && resetPlayer.Entity.Tags.Single(tag => tag.Name == 4017).Value == pet.Entity
                    && resetPlayer.Entity.Tags.Single(tag => tag.Name == 4037).Value == 5,
                    "each rewind reset reinjects one matching pet using the same entity ID and player corner variant");
            }
            profile.RecoverInterrupted(); profile.SelectPet(null); profile.Begin("mage", "priest", 72); profile.SelectPet("PET_3_1");
            packets.Clear();
            var none = new NativeMatch((id, packet) => { if (id == 19) packets.Add((PowerHistory)packet); }, "mage", "priest", 72); none.Start();
            var noneInitial = packets.SelectMany(packet => packet.List).ToArray();
            Core2026Proof.Check(!noneInitial.Any(entry => entry.HasFullEntity && entry.FullEntity.Name?.StartsWith("PET_") == true)
                && noneInitial.Single(entry => entry.HasCreateGame).CreateGame.Players.All(player =>
                    !player.Entity.Tags.Any(tag => tag.Name == 4017 || tag.Name == 4037)),
                "frozen None stays without a pet entity or player corner variant after a later pet selection");
            var noneReset = new PowerHistory();
            var noneEntries = new List<IPowerHistoryEntry> {
                PowerHistoryBuilder.BlockStart(SabberStoneCore.Enums.BlockType.GAME_RESET, none.Game.Player1.Hero.Id, "", -1, 0),
                PowerHistoryBuilder.ResetGame(none.Game) };
            noneEntries.AddRange(none.Game.IdEntityDic.Values.OrderBy(entity => entity.Id).Select(entity => PowerHistoryBuilder.FullEntity(entity, reset: true)));
            noneEntries.Add(PowerHistoryBuilder.BlockEnd());
            append.Invoke(none, new object[] { noneReset, noneEntries, 0 });
            Core2026Proof.Check(!noneReset.List.Any(entry => entry.HasFullEntity && entry.FullEntity.Name?.StartsWith("PET_") == true)
                && noneReset.List.Single(entry => entry.HasResetGame).ResetGame.CreateGame.Players.All(player =>
                    !player.Entity.Tags.Any(tag => tag.Name == 4017 || tag.Name == 4037)),
                "rewinding frozen None cannot inject a pet entity or player corner variant");
            // PROPOSED NPC opponent pet (docs/design/npc-pets.md): a second cosmetic entity for controller 2, linked by the NPC player.
            NativeMatch.NativePetVariant = card => card == "PET_3_1" ? 5 : card == "PET_4_1" ? 9 : throw new Exception("Unexpected form");
            profile.RecoverInterrupted(); profile.SelectPet("PET_3_1"); profile.Begin("mage", "priest", 73);
            packets.Clear();
            var duo = new NativeMatch((id, packet) => { if (id == 19) packets.Add((PowerHistory)packet); }, "mage", "priest", 73) { OpponentPetCard = "PET_4_1" };
            duo.Start();
            var duoInitial = packets.SelectMany(packet => packet.List).ToArray();
            var humanPet = duoInitial.Single(entry => entry.HasFullEntity && entry.FullEntity.Name == "PET_3_1").FullEntity;
            var npcPet = duoInitial.Single(entry => entry.HasFullEntity && entry.FullEntity.Name == "PET_4_1").FullEntity;
            int NpcTag(int name) => npcPet.Tags.Single(tag => tag.Name == name).Value;
            Core2026Proof.Check(humanPet.Entity != npcPet.Entity && humanPet.Tags.Single(tag => tag.Name == 50).Value == 1
                && NpcTag(202) == 45 && NpcTag(49) == 9 && NpcTag(50) == 2 && NpcTag(4079) == 4 && NpcTag(4037) == 9,
                "NPC pet is a distinct cosmetic entity controlled by player 2 with exact family and variant");
            var duoPlayers = duoInitial.Single(entry => entry.HasCreateGame).CreateGame.Players;
            Core2026Proof.Check(duoPlayers.Single(player => player.Id == 2).Entity.Tags.Single(tag => tag.Name == 4017).Value == npcPet.Entity
                && duoPlayers.Single(player => player.Id == 2).Entity.Tags.Single(tag => tag.Name == 4037).Value == 9
                && duoPlayers.Single(player => player.Id == 1).Entity.Tags.Single(tag => tag.Name == 4017).Value == humanPet.Entity,
                "NPC player links its own pet and opposing corner variant; the human link is unchanged");
            for (int attempt = 0; attempt < 2; attempt++) {
                var entries = new List<IPowerHistoryEntry> { PowerHistoryBuilder.BlockStart(SabberStoneCore.Enums.BlockType.GAME_RESET, duo.Game.Player1.Hero.Id, "", -1, 0),
                    PowerHistoryBuilder.ResetGame(duo.Game) };
                entries.AddRange(duo.Game.IdEntityDic.Values.OrderBy(entity => entity.Id).Select(entity => PowerHistoryBuilder.FullEntity(entity, reset: true)));
                entries.Add(PowerHistoryBuilder.BlockEnd());
                var reset = new PowerHistory(); append.Invoke(duo, new object[] { reset, entries, 0 });
                Core2026Proof.Check(reset.List.Count(entry => entry.HasFullEntity && entry.FullEntity.Name == "PET_3_1" && entry.FullEntity.Entity == humanPet.Entity) == 1
                    && reset.List.Count(entry => entry.HasFullEntity && entry.FullEntity.Name == "PET_4_1" && entry.FullEntity.Entity == npcPet.Entity) == 1
                    && reset.List.Single(entry => entry.HasResetGame).ResetGame.CreateGame.Players.Single(player => player.Id == 2).Entity.Tags.Single(tag => tag.Name == 4017).Value == npcPet.Entity,
                    "each rewind reset reinjects both pets with unchanged ids and the NPC player link");
            }
            Console.WriteLine("PASS pet transport: frozen pet/None, exact native IDs, player link/corner variant, repeated rewind reinjection without duplicate entities");
        } finally {
            NativeMatch.CosmeticsProfile = previousProfile; NativeMatch.NativePetVariant = previousVariant; LocalShop.TestOverride = previousShop;
            Directory.Delete(root, true);
        }
    }
}
