using System.IO.Compression;
using System.Text.Json;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using static Core2026Proof;
using static CostSliceProof;
using static PoolPilotProof;

internal static class JackpotProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_TID_931", (g, p, id) => {
        CoreCostProof.Count(g, id);
        var clone = g.CloneExact(history: true); Play(clone, clone.Player1, id); Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 2, "two expensive spells");
        Core2026Proof.Check(p.HandZone.Select(c => c.Card.Id).SequenceEqual(clone.Player1.HandZone.Select(c => c.Card.Id)), "seeded independent picks deterministic");
        using var file = File.OpenRead("data/carddb/37.0.0.254477/cards.json.gz");
        using var gzip = new GZipStream(file, CompressionMode.Decompress); using var json = JsonDocument.Parse(gzip);
        var definitions = json.RootElement.GetProperty("cards").EnumerateArray().ToDictionary(c => c.GetProperty("cardId").GetString());
        bool Eligible(string cardId, string hero)
        {
            var row = definitions[cardId]; var classes = row.GetProperty("cardClasses").EnumerateArray().Select(c => c.GetString()).ToArray();
            return row.GetProperty("cardType").GetString() == "SPELL" && row.GetProperty("cost").GetInt32() >= 5
                && !classes.Contains(hero) && !classes.Contains("NEUTRAL") && cardId != id;
        }
        Core2026Proof.Check(p.HandZone.All(c => Eligible(c.Card.Id, "ROGUE")), "every Rogue output obeys export cardClasses");
        Core2026Proof.Check(definitions["END_023"].GetProperty("cardClasses").EnumerateArray().Select(c => c.GetString())
            .SequenceEqual(new[] { "MAGE", "WARRIOR" }), "export fixture has both Mage and Warrior membership");
        foreach (var heroId in new[] { "HERO_03", "HERO_08", "HERO_01" })
        {
            p.AddHeroAndPower(Cards.FromId(heroId)); string hero = p.HeroClass.ToString();
            var expected = g.OfflinePools.Standard.Where(m => Eligible(m.CardId, hero)).Select(m => m.CardId).ToHashSet();
            var candidates = OfflineCardPools.Candidates(g, p.HeroClass, Filter(id), Cards.FromId(id).AssetId);
            Core2026Proof.Check(expected.SetEquals(candidates.Select(c => c.Id))
                && expected.Contains("END_023") == (hero == "ROGUE"), "complete export class filter including secondary Warrior class");
            for (int i = 0; i < 32; i++)
            {
                EmptyHand(p); long draws = g.Random.DrawCount; Play(g, p, id);
                Core2026Proof.Check(p.HandZone.Count == 2 && p.HandZone.All(c => Eligible(c.Card.Id, hero))
                    && g.Random.DrawCount == draws + 2, "every generated identity excludes current controller class " + hero);
            }
            LostCityPoolProof.WithPool(g, id, hero == "ROGUE" ? new[] { "END_023" } : Array.Empty<string>(), () => {
                EmptyHand(p); long draws = g.Random.DrawCount; Play(g, p, id);
                int expectedCount = hero == "ROGUE" ? 2 : 0;
                Core2026Proof.Check(p.HandZone.Count == expectedCount && p.HandZone.All(c => c.Card.Id == "END_023")
                    && g.Random.DrawCount == draws + expectedCount, "multiclass spell generated only for unrelated controller");
            }, dependencies: new[] { "END_023" });
        }
        p.AddHeroAndPower(Cards.FromId("HERO_03")); EmptyHand(p);
        HandBoundaries(g, p, id, 2);
    });
}
