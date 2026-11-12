using System.Reflection;

internal static class HeroSkinRewardChecks
{
    internal static void Run(string root)
    {
        var grant = typeof(LocalProfile).GetMethod("GrantClassRewards", BindingFlags.Static | BindingFlags.NonPublic)!;
        using var profile = new LocalProfile(Path.Combine(root, "honored-auto-equip.xml"));
        foreach (var portrait in LocalClassRewards.Portraits)
        foreach (string choice in new[] { (string)null, portrait.Hero,
            AchievementExclusiveCosmetics.All.Single(row => row.Class == portrait.Hero).HeroSkinId })
        {
            var data = profile.FeatsSnapshot();
            if (choice != null) data.FavoriteHeroSkins.Add(new LocalHeroFavorite { Class = portrait.Hero, HeroCardId = choice });
            // Exercise settlement's real grant function at the ranked-win threshold, independent of achievement grants.
            data.Matches = Enumerable.Range(0, LocalClassRewards.HonoredPortraitWins).Select(_ => new LocalMatchRecord {
                DeckHero = portrait.Hero, Result = "Won", ClassXpBefore = 0, ClassXpAfter = 1
            }).ToList();
            grant.Invoke(null, new object[] { data, null });
            if (!data.OwnedHeroSkins.Contains(portrait.Honored)
                || data.FavoriteHeroSkins.Single(row => row.Class == portrait.Hero).HeroCardId != (choice ?? portrait.Honored))
                throw new Exception("Honored portrait must auto-equip only without an explicit choice: " + portrait.Hero);
            int count = data.FavoriteHeroSkins.Count;
            grant.Invoke(null, new object[] { data, null });
            if (data.FavoriteHeroSkins.Count != count) throw new Exception("Honored portrait grant replay changed favourites");
        }
        Console.WriteLine("PASS honored portrait grants: all classes auto-equip, explicit base/skin kept, idempotence");
    }
}
