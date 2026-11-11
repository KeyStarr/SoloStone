using System.IO.Compression;
using System.Text.Json;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, exact current card text/tags.
// Tier A: Blizzard 15.2 / 18.0.1 Discover rules; see CorePoolProof.
// Tier C: https://hearthstone.wiki.gg/wiki/Ivory_Knight#Notes — healing uses base Cost before modifiers.
internal static class IvoryKnightProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_KAR_057", (g, p, id) => {
        CorePoolProof.CrossClass(g, id);
        using var gzip = new GZipStream(File.OpenRead("data/carddb/37.0.0.254477/cards.json.gz"), CompressionMode.Decompress);
        using var export = JsonDocument.Parse(gzip);
        var printed = export.RootElement.GetProperty("cards").EnumerateArray().ToDictionary(c => c.GetProperty("cardId").GetString());
        p.Hero.Damage = 20; p.Opponent.Hero.Damage = 10;
        Play(g, p, id);
        Core2026Proof.Check(p.Choice != null && p.Choice.Choices.Count == 3 && p.Choice.Choices.Distinct().Count() == 3 && p.Choice.Choices.All(key => g.IdEntityDic[key].Card.Type == CardType.SPELL), "three distinct spell choices");
        foreach (int offer in p.Choice.Choices)
        {
            var entity = g.IdEntityDic[offer]; var data = printed[entity.Card.Id];
            Core2026Proof.Check(entity.Card.AssetId == data.GetProperty("dbfId").GetInt32() && entity.Card.Cost == data.GetProperty("cost").GetInt32(),
                "exact pinned offered identity/Cost: " + entity.Card.Id + " actual=" + entity.Card.Cost + " export=" + data.GetProperty("cost"));
        }
        int key = p.Choice.Choices.First(k => g.IdEntityDic[k].Card.Cost > 0); var chosen = g.IdEntityDic[key].Card; int cost = chosen.Cost;
        Core2026Proof.Check(cost > 0, "fixture chooses nonzero Cost spell to detect missing heal");
        PoolPilotProof.Resume(g, next => {
            CoreCostProof.Answer(next, key);
            Core2026Proof.Check(next.Player1.HandZone.Single().Card.Id == chosen.Id && next.Player1.Hero.Damage == Math.Max(0, 20 - cost) && next.Player2.Hero.Damage == 10, "chosen exact spell enters friendly hand and heals printed Cost only");
        });
        PoolPilotProof.EmptyHand(p); p.Hero.Damage = 1; Play(g, p, id);
        key = p.Choice.Choices.First(k => g.IdEntityDic[k].Card.Cost > 0);
        CoreCostProof.Answer(g, key);
        Core2026Proof.Check(p.Hero.Damage == 0, "healing capped at maximum Health");
        PoolPilotProof.EmptyHand(p); var source = Add(p, id); PoolPilotProof.FillHand(p);
        PlayEntity(g, p, source); PoolPilotProof.FillHand(p); p.Hero.Damage = 20;
        key = p.Choice.Choices.First(k => g.IdEntityDic[k].Card.Cost > 0); cost = g.IdEntityDic[key].Card.Cost;
        CoreCostProof.Answer(g, key);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.Hero.Damage == Math.Max(0, 20 - cost), "full hand burns chosen card but still heals");
        PoolPilotProof.EmptyHand(p); p.Hero.Damage = 20; Play(g, p, id);
        key = p.Choice.Choices.First(k => g.IdEntityDic[k].Card.Cost > 0);
        int baseCost = g.IdEntityDic[key].Card.Cost;
        g.IdEntityDic[key].Cost = 0;
        Core2026Proof.Check(g.IdEntityDic[key].Cost == 0 && baseCost > 0, "discounted offer differs from base Cost");
        PoolPilotProof.Resume(g, next => {
            CoreCostProof.Answer(next, key);
            Core2026Proof.Check(next.Player1.Hero.Damage == 20 - baseCost, "healing uses base Cost, independent of entity cost modifiers");
        });
    }, legacyPoolMember: true);
}
