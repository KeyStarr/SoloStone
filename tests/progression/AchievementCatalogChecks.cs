using System.Xml.Serialization;
using System.Security.Cryptography;
using System.Text;

internal static class AchievementCatalogChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    internal static void Run()
    {
        string[] expectedIds = {
            "calculated", "out-of-cards-not-ideas", "seven-card-pickup", "thanks-ill-take-it-from-here",
            "mutually-assured-destruction", "aggressively-average", "call-an-ambulance", "fine-ill-do-it-myself",
            "absolutely-unnecessary", "death-knight-grave-expenditure", "demon-hunter-face-value",
            "druid-ahead-of-the-curve", "hunter-whos-a-good-boy", "mage-final-exam", "paladin-entry-level-position",
            "priest-we-can-rebuild-you", "rogue-combo-meal", "shaman-the-whole-band", "warlock-damage-is-a-resource",
            "warrior-good-stats-for-the-cost", "no-copies-no-problem", "creature-feature", "words-not-violence",
            "no-famous-people", "home-team", "neutral-ground", "small-ball", "heavy-reading", "one-expansion-stand",
            "perfect-curve", "respectfully-no", "from-downtown", "armored-division", "untouchable",
            "one-of-everything", "family-reunion", "topdecking-is-a-skill", "hostile-takeover", "no-time-to-explain",
            "running-on-fumes", "population-boom", "acceptable-losses", "no-touching", "chain-reaction", "power-trip",
            "pocket-sized-problem", "not-today", "special-delivery", "borrowed-time", "return-to-sender",
            "a-hundred-good-games", "class-act", "well-read", "the-board-is-a-workplace-hazard", "the-complete-works",
            "world-tour", "made-to-measure", "welcome-to-legend", "among-the-hundred", "there-can-be-only-one"
        };
        var catalog = LocalAchievementCatalog.All;
        var milestones = catalog.Skip(60).ToArray();
        Check(catalog.Count == 64 && catalog.Select(row => row.Id).Distinct().Count() == 64
            && milestones.Select(row => row.Id).SequenceEqual(new[] { "first-signature-mastery", "first-diamond-mastery", "fifty-golden-masteries", "all-premium-deck" })
            && milestones.All(row => row.Reward.GrantKindCount == 0), "mastery milestone catalog changed");
        var all = catalog.Take(60).ToArray();
        Check(all.Length == 60 && all.Select(x => x.Id).SequenceEqual(expectedIds), "achievement ids/order changed");
        Check(all.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() == 60, "achievement ids not unique");
        string catalogText = string.Join("\n", all.Select(x => string.Join("\u001f", x.Id, x.Name, x.Condition,
            x.Reward.Gold, x.Reward.Dust, x.Reward.StandardPacks, x.Reward.CardBackId,
            x.Reward.HeroSkinId ?? "", x.Reward.LinkedMilestoneId ?? "")));
        string catalogHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(catalogText))).ToLowerInvariant();
        Check(catalogHash == "5e3f0c54b34a38c217d3fcd229185f95d711ab94a2a9f44bc8fa6a3532f773c8",
            "achievement names, conditions, or per-entry rewards changed: " + catalogHash);

        int portraits = all.Count(x => x.Reward.HeroSkinId != null);
        int cardBacks = all.Count(x => x.Reward.CardBackId != 0);
        int linked = all.Count(x => x.Reward.LinkedMilestoneId != null);
        int currency = all.Count(x => x.Reward.Gold != 0 || x.Reward.Dust != 0 || x.Reward.StandardPacks != 0);
        Check(portraits == 11 && cardBacks == 6 && linked == 3 && currency == 40, "achievement reward split changed");
        Check(portraits + cardBacks + linked == 20, "cosmetic-bearing achievement count changed");
        Check(all.Skip(57).All(x => x.Reward.LinkedMilestoneId != null && x.Reward.GrantKindCount == 1),
            "entries 58-60 must be link-only");
        Check(all.Take(57).All(x => x.Reward.LinkedMilestoneId == null), "unexpected linked milestone");
        Check(all.Where(x => x.Reward.Gold != 0 || x.Reward.Dust != 0 || x.Reward.StandardPacks != 0)
            .All(x => x.Reward.CardBackId == 0 && x.Reward.HeroSkinId == null && x.Reward.LinkedMilestoneId == null),
            "currency/pack reward also grants a cosmetic");
        Check(all.All(x => x.Reward.GrantKindCount == 1), "achievement must have exactly one grant kind");
        Check(all.Where(x => x.Reward.CardBackId != 0).Select(x => "cardback:" + x.Reward.CardBackId)
            .Concat(all.Where(x => x.Reward.HeroSkinId != null).Select(x => "hero:" + x.Reward.HeroSkinId))
            .Distinct(StringComparer.Ordinal).Count() == portraits + cardBacks, "exclusive cosmetic reused");

        var first = LocalAchievementCatalog.Get(expectedIds[0]);
        var second = LocalAchievementCatalog.Get(expectedIds[0]);
        Check(!ReferenceEquals(first, second) && !ReferenceEquals(first.Reward, second.Reward), "catalog leaks shared mutable objects");
        Check(first.Id == second.Id && first.Reward.CardBackId == 519, "catalog defensive copy changed values");
        Check(LocalAchievementCatalog.Get("missing") == null, "unknown achievement resolved");

        var receipt = LocalAchievementGrantReceipt.FromCatalog("calculated");
        Check(receipt.AchievementId == "calculated" && receipt.CatalogVersion == LocalAchievementCatalog.Version
            && receipt.CardBackId == 519 && receipt.Gold == 0 && receipt.Dust == 0 && receipt.StandardPacks == 0,
            "grant receipt not sourced from catalog");
        var state = new LocalAchievementState {
            CatalogVersion = LocalAchievementCatalog.Version,
            Unlocks = new() { new LocalAchievementUnlock { Id = "calculated", UnlockedUtc = "2026-10-26T00:00:00Z", MatchId = "m1" } },
            Grants = new() { receipt },
            Career = new LocalAchievementCareer { EligibleWins = 7, CreditedEnemyMinionDeaths = 3 }
        };
        using var writer = new StringWriter();
        new XmlSerializer(typeof(LocalAchievementState)).Serialize(writer, state);
        using var reader = new StringReader(writer.ToString());
        var copy = (LocalAchievementState)new XmlSerializer(typeof(LocalAchievementState)).Deserialize(reader);
        Check(copy.CatalogVersion == state.CatalogVersion && copy.Unlocks.Single().MatchId == "m1"
            && copy.Grants.Single().CardBackId == 519 && copy.Career.EligibleWins == 7, "achievement state XML roundtrip failed");
    }
}
