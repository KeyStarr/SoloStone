using System.Xml.Linq;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class CataclysmPoolProof
{
    internal static OfflinePoolFilter Filter(string id, int? cost = null) => OfflinePoolDeclarations.Cards.Single(c => c.CardId == id).Pools
        .First(p => cost == null || p.Filter.MinCost == cost).Filter;

    internal static void WithPool(Game g, string id, int count, Action action, int? cost = null)
    {
        var installed = g.OfflinePools;
        var filter = Filter(id, cost);
        var candidates = OfflineCardPools.Candidates(g, g.Player1.HeroClass, filter, Cards.FromId(id).AssetId);
        var xml = OfflinePoolFixtures.Catalog();
        var rows = xml.Root.Element("Members");
        var selected = count == 0
            ? rows.Elements("Card").Where(r => (string)r.Attribute("type") == "WEAPON").Take(1).ToArray()
            : rows.Elements("Card").Where(r => candidates.Take(count).Any(c => c.Id == (string)r.Attribute("cardId"))).ToArray();
        rows.ReplaceNodes(selected);
        var manifest = OfflinePoolFixtures.Manifest(xml);
        Core2026Proof.Check(OfflineCardPools.Candidates(manifest, g.Player1.HeroClass, filter, Cards.FromId(id).AssetId).Count == count, "fixture pool size");
        var property = typeof(Game).GetProperty(nameof(Game.OfflinePools));
        try { property.SetValue(g, manifest); action(); }
        finally { property.SetValue(g, installed); }
    }

    internal static void Generated(Game g, string recipe, IPlayable source, IEnumerable<IPlayable> outputs, int count,
        Zone zone, int discount = 0, bool doubled = false)
    {
        var cards = outputs.ToArray();
        var candidates = OfflinePoolDeclarations.Cards.Single(c => c.CardId == recipe).Pools
            .SelectMany(p => OfflineCardPools.Candidates(g, source.Controller.HeroClass, p.Filter, source.Card.AssetId)).ToArray();
        Core2026Proof.Check(cards.Length == count && cards.Select(c => c.Id).Distinct().Count() == count, "generated count and unique entity ids");
        foreach (var card in cards)
        {
            Core2026Proof.Check(candidates.Any(c => c.Id == card.Card.Id && c.AssetId == card.Card.AssetId) && card.Card.Id != source.Card.Id,
                "generated declared pool member; runtime source excluded");
            Core2026Proof.Check(g.IdEntityDic[card.Id] == card && card.Controller == source.Controller && card.Zone.Type == zone
                && card[GameTag.DISPLAYED_CREATOR] == source.Id && card.ZonePosition >= 0, "generated owner/zone/position/creator");
            var member = g.OfflinePools.Standard.Single(m => m.CardId == card.Card.Id);
            Core2026Proof.Check(card.Card.Cost == member.Cost && card.Card.ATK == member.Attack && card.Card.Health == member.Health,
                "generated pinned stats");
            int cost = card.Card.Cost - discount;
            if (card.Card.Id == "JAIL_514") cost -= card.Controller.HandZone.Count;
            if (card.Card.Id == "JAIL_501") cost = card.Controller.RemainingMana - discount;
            Core2026Proof.Check(card.Cost == Math.Max(0, cost) && card[GameTag.COST] == card.Cost, "generated visible cost: " + card.Card.Id + ":" + card.Cost + "/tag=" + card[GameTag.COST] + "/expected=" + Math.Max(0,cost));
            Core2026Proof.Check(recipe switch {
                "CATA_556" => card.Card.Type == CardType.MINION && card.Card.IsRace(Race.DRAGON) && card.Card.Cost <= 3,
                "MEND_045" => card.Card.Type == CardType.MINION && card.Card.IsRace(Race.DRAGON),
                "CATA_136" => card.Card.Type == CardType.MINION && card.Card.Cost >= 8,
                "CATA_471" => card.Card.Type == CardType.MINION && card.Card.Cost == 4,
                "CATA_499" => card.Card.Type == CardType.MINION && card.Card.Cost == 1,
                "CATA_569" => card.Card.Type == CardType.MINION && card.Card.Cost >= 1 && card.Card.Cost <= 3,
                "MEND_042" => card.Card.Type == CardType.MINION && card.Card.Cost == 8,
                "CATA_474" => card.Card.Type == CardType.SPELL && card.Card.Tags.GetValueOrDefault((GameTag)1635) == 5,
                _ => false
            }, "generated card meets printed pool constraint");
            if (card is not Minion minion) continue;
            int attack = minion.Card.ATK * (doubled ? 2 : 1);
            if (minion.Zone == minion.Controller.BoardZone && minion.Card.Id == "JAIL_311" && minion.Controller.DeckZone.Count >= 25) attack += 5;
            attack += minion.AuraEffects?.ATK ?? 0;
            if (minion.Card.Id == "JAIL_501" && minion.Zone == minion.Controller.HandZone) attack = minion.Controller.RemainingMana;
            int health = minion.Card.Health * (doubled ? 2 : 1) + (minion.AuraEffects?.Health ?? 0);
            if (minion.Card.Id == "JAIL_501" && minion.Zone == minion.Controller.HandZone) health = minion.Controller.RemainingMana;
            Core2026Proof.Check(minion.AttackDamage == attack && minion.Health == health,
                $"generated effective stats {card.Card.Id}: {minion.AttackDamage}/{minion.Health}, expected {attack}/{health}");
        }
    }

    internal static void PutBoardAside(Controller p)
    {
        foreach (var minion in p.BoardZone.ToArray()) p.SetasideZone.Add(p.BoardZone.Remove(minion));
        foreach (var location in p.BoardZone.GetLocations().ToArray()) p.SetasideZone.Add(p.BoardZone.Remove(location));
        p.Game.DeathProcessingAndAuraUpdate();
    }
}
