using System.Xml.Linq;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class EmeraldDreamPoolProof
{
    internal static OfflinePoolFilter Filter(string id) => OfflinePoolDeclarations.Cards.Single(c => c.CardId == id).Pools.Single().Filter;

    internal static void WithPool(Game g, string id, int count, Action action)
    {
        var installed = g.OfflinePools;
        var filter = Filter(id);
        var candidates = OfflineCardPools.Candidates(g, g.Player1.HeroClass, filter, Cards.FromId(id).AssetId);
        var xml = OfflinePoolFixtures.Catalog();
        var rows = xml.Root.Element("Members");
        var selected = count == 0
            ? rows.Elements("Card").Where(r => !candidates.Any(c => c.Id == (string)r.Attribute("cardId"))).Take(1).ToArray()
            : rows.Elements("Card").Where(r => candidates.Take(count).Any(c => c.Id == (string)r.Attribute("cardId"))).ToArray();
        rows.ReplaceNodes(selected);
        var manifest = OfflinePoolFixtures.Manifest(xml);
        Core2026Proof.Check(OfflineCardPools.Candidates(manifest, g.Player1.HeroClass, filter, Cards.FromId(id).AssetId).Count == count, "fixture pool size");
        var property = typeof(Game).GetProperty(nameof(Game.OfflinePools));
        try { property.SetValue(g, manifest); action(); }
        finally { property.SetValue(g, installed); }
    }

    internal static void Generated(Game g, IPlayable source, IEnumerable<IPlayable> outputs, int count, int discount = 0)
    {
        var cards = outputs.ToArray();
        var candidates = OfflineCardPools.Candidates(g, source.Controller.HeroClass, Filter(source.Card.Id), source.Card.AssetId);
        Core2026Proof.Check(cards.Length == count && cards.Select(c => c.Id).Distinct().Count() == count, "generated count/unique entities");
        foreach (var card in cards)
        {
            Core2026Proof.Check(candidates.Any(c => c.Id == card.Card.Id && c.AssetId == card.Card.AssetId) && card.Card.Id != source.Card.Id,
                "declared pool identity and source exclusion");
            Core2026Proof.Check(g.IdEntityDic[card.Id] == card && card.Controller == source.Controller && card.Zone == source.Controller.HandZone
                && card[GameTag.DISPLAYED_CREATOR] == source.Id && card.ZonePosition >= 0, "generated owner/zone/position/creator");
            var member = g.OfflinePools.Standard.Single(m => m.CardId == card.Card.Id);
            Core2026Proof.Check(card.Card.Cost == member.Cost && card.Card.ATK == member.Attack && card.Card.Health == member.Health, "pinned generated stats");
            Core2026Proof.Check(source.Card.Id switch {
                "EDR_999" => card.Card.Type == CardType.MINION && card.Card.IsRace(Race.MURLOC),
                "EDR_462" => card.Card.Type == CardType.MINION && card.Card.IsRace(Race.DRAGON),
                "FIR_913" => card.Card.Type == CardType.MINION && card.Card.IsRace(Race.ELEMENTAL),
                _ => false
            }, "printed pool constraint");
            Core2026Proof.Check(card.Cost == Math.Max(0, card.Card.Cost - discount - (card.Card.Id == "FIR_919" ? card.Controller.NumCardsPlayedThisTurn : 0)) && card[GameTag.COST] == card.Cost, $"visible generated cost: {card.Card.Id} cost={card.Cost} tag={card[GameTag.COST]} printed={card.Card.Cost} discount={discount}");
            if (card is Minion minion)
                Core2026Proof.Check(minion.AttackDamage == minion.Card.ATK && minion.Health == minion.Card.Health, "generated effective stats");
        }
    }

    internal static void RandomHandBoundaries(Game g, Controller p, string id, Action action)
    {
        foreach (int count in new[] { 0, 1, 2 })
        {
            PoolPilotProof.EmptyHand(p);
            WithPool(g, id, count, () => {
                long draws = g.Random.DrawCount; int entities = g.IdEntityDic.Count;
                action();
                Core2026Proof.Check(p.HandZone.Count == (count == 0 ? 0 : 1) && g.Random.DrawCount - draws == (count == 0 ? 0 : 1), "empty/tiny random pool");
                if (count == 0) Core2026Proof.Check(g.IdEntityDic.Count == entities, "empty pool creates no entity");
            });
        }
        PoolPilotProof.EmptyHand(p); PoolPilotProof.FillHand(p);
        long before = g.Random.DrawCount; action();
        Core2026Proof.Check(p.HandZone.Count == 10 && g.Random.DrawCount == before + 1, "full hand still samples without overflow");
        PoolPilotProof.EmptyHand(p);
    }
}
