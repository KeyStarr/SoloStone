using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Serialization;
using PegasusGame;

internal static class CosmeticsMatchTests
{
    internal static void Run()
    {
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); }
        using var fixture = new CosmeticsFixture();
        string path = Path.Combine(Path.GetTempPath(), "cosmetics-match-" + Guid.NewGuid() + ".xml");
        try {
            using (var p = new LocalProfile(path)) p.SelectCardBack(0);
            var serializer = new XmlSerializer(typeof(LocalProfileData));
            LocalProfileData data;
            using (var file = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(file);
            data.GoldBalance = 10000;
            using (var file = File.Create(path)) serializer.Serialize(file, data);
            using var profile = new LocalProfile(path);
            var product = LocalShop.Default.Products.Single(p => p.HeroSkin == "HERO_08a");
            profile.Purchase(product.Id, 1, 1000, "match-skin");
            profile.SetFavoriteHeroSkin("HERO_08", "HERO_08a");
            NativeMatch.CosmeticsProfile = () => profile;
            profile.Purchase("cardback-122", 1, 500, "match-back");
            profile.SelectCardBack(122);
            var packets = new List<(int, IProtoBuf)>();
            var match = new NativeMatch((id, message) => packets.Add((id, message)), "mage", "warrior", 1337);
            match.Start();
            var histories = packets.Where(p => p.Item2 is PowerHistory).Select(p => (PowerHistory)p.Item2);
            var create = histories.SelectMany(h => h.List).Select(h => h.CreateGame).First(c => c != null);
            Check(create.Players.Single(p => p.Id == 1).CardBack == 122, "Match uses selected player card back");
            Check(create.Players.Single(p => p.Id == 2).CardBack != 122, "Opponent card back unchanged");
            var hero = histories.SelectMany(h => h.List).Select(h => h.FullEntity).Where(e => e != null)
                .First(e => e.Entity == match.Game.Player1.Hero.Id);
            Check(hero.Name == "HERO_08a", "Player skin reaches native hero entity");
            Check(match.Game.Player1.Hero.Card.Id == "HERO_08" && match.Game.Player1.HeroPowers.Single().Card.Id == "CS2_034", "Skin preserves engine class and power rules");
            Check(match.Game.Player2.Hero.Card.Id == "HERO_01", "Opponent hero unchanged");
            Console.WriteLine("PASS native player cosmetics and unchanged gameplay/opponent");
        } finally {
            NativeMatch.CosmeticsProfile = null;
            foreach (string file in Directory.GetFiles(Path.GetDirectoryName(path), Path.GetFileName(path) + "*")) File.Delete(file);
        }
    }
}
