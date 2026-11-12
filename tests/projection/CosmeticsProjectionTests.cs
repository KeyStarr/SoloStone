using System.Reflection;
using System.Xml.Serialization;

internal static class CosmeticsProjectionTests
{
    internal static void Run()
    {
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); }
        object Call(string method, params object[] args) {
            var found = typeof(NativeUIProjection).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic);
            Check(found != null, "Missing cosmetics projection: " + method);
            return found!.Invoke(null, args)!;
        }
        using var fixture = new CosmeticsFixture(coins: true);
        string path = Path.Combine(Path.GetTempPath(), "cosmetics-" + Guid.NewGuid() + ".xml");
        try {
            using (var profile = new LocalProfile(path)) profile.SelectCardBack(0);
            var serializer = new XmlSerializer(typeof(LocalProfileData));
            LocalProfileData data;
            using (var file = File.OpenRead(path)) data = (LocalProfileData)serializer.Deserialize(file)!;
            data.GoldBalance = 10000;
            using (var file = File.Create(path)) serializer.Serialize(file, data);
            using (var profile = new LocalProfile(path)) {
                Check(((IEnumerable<string>)Call("CosmeticHeroes", profile)).Count() == 11, "Base heroes always projected");
                Check((string)Call("FavoriteHero", profile, "HERO_08") == "HERO_08", "Unset favourite uses base hero");
                var product = LocalShop.Default.Products.Single(p => p.HeroSkin == "HERO_08a");
                profile.Purchase(product.Id, 1, 1000, "cosmetic");
                profile.Purchase("cardback-122", 1, 500, "back");
                profile.Purchase("coin-2", 1, 300, "coin");
                NativeCoinsHarness.Run(profile, Check);
                Call("SelectCardBack", profile, 122, true);
                Check(profile.FavoriteCardBacks.SequenceEqual(new[] { 0, 122 }), "Owned nondefault back selection commits");
                Check(((IEnumerable<string>)Call("CosmeticHeroes", profile)).Count() == 12, "Post-purchase projection includes skin once");
                Call("SelectHeroSkin", profile, "HERO_08", "HERO_08a", true);
                Check((string)Call("FavoriteHero", profile, "HERO_08") == "HERO_08a", "Owned selection committed");
                byte[] before = File.ReadAllBytes(path);
                try { Call("SelectHeroSkin", profile, "HERO_01", "HERO_08a", true); throw new Exception("Wrong class accepted"); }
                catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { }
                try { Call("SelectHeroSkin", profile, "HERO_01", "HERO_01a", true); throw new Exception("Unowned skin accepted"); }
                catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { }
                try { Call("SelectCardBack", profile, 999, true); throw new Exception("Unowned back accepted"); }
                catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { }
                Check(File.ReadAllBytes(path).SequenceEqual(before), "Refusal leaves durable selection unchanged");
                profile.CommitFailure = _ => throw new IOException("selection write failure");
                try { Call("SelectHeroSkin", profile, "HERO_08", "HERO_08a", false); throw new Exception("Failed write accepted"); }
                catch (TargetInvocationException e) when (e.InnerException is IOException) { }
                Check(profile.FavoriteHeroSkins["HERO_08"] == "HERO_08a" && File.ReadAllBytes(path).SequenceEqual(before), "Failed selection leaves memory/file unchanged");
                profile.CommitFailure = null;
            }
            using (var profile = new LocalProfile(path)) {
                Check((string)Call("FavoriteHero", profile, "HERO_08") == "HERO_08a", "Selected skin survives restart");
                Call("SelectHeroSkin", profile, "HERO_08", "HERO_08a", false);
                Check((string)Call("FavoriteHero", profile, "HERO_08") == "HERO_08", "Unfavorite restores always-owned base hero");
                Check(profile.FavoriteCardBacks.SequenceEqual(new[] { 0, 122 }), "Selected card back survives restart");
                Call("SelectCardBack", profile, 122, false);
                Check(profile.FavoriteCardBacks.Single() == 0, "Unfavorite default still selects default");
            }
            Console.WriteLine("PASS cosmetics ownership, purchase refresh, selection persistence and refusal");
        } finally { foreach (string file in Directory.GetFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + "*")) File.Delete(file); }
    }
}
