using System.Xml.Linq;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class Batch9PoolProof
{
    internal static OfflinePoolFilter Filter(string id, int index = 0) =>
        OfflinePoolDeclarations.Cards.Single(c => c.CardId == id).Pools[index].Filter;

    internal static void Generated(Game g, IPlayable source, IEnumerable<IPlayable> output, int count,
        int index = 0, string recipe = null)
    {
        var cards = output.ToArray();
        var pool = OfflineCardPools.Candidates(g, source.Controller.HeroClass, Filter(recipe ?? source.Card.Id, index), source.Card.AssetId);
        Core2026Proof.Check(cards.Length == count && cards.Select(c => c.Id).Distinct().Count() == count, "generated distinct entities and count");
        foreach (var card in cards)
        {
            Core2026Proof.Check(pool.Any(c => c.Id == card.Card.Id && c.AssetId == card.Card.AssetId) && card.Card.Id != source.Card.Id,
                "generated identity matches declared pool; source excluded");
            Core2026Proof.Check(card.Controller == source.Controller && card[GameTag.DISPLAYED_CREATOR] == source.Id &&
                (card.Zone == source.Controller.HandZone || card.Zone == source.Controller.BoardZone), "generated controller, creator and zone");
            Core2026Proof.Check(card.Id > source.Id && g.IdEntityDic[card.Id] == card && card.ZonePosition >= 0, "generated live entity identity and position");
            var member = g.OfflinePools.Standard.Single(c => c.CardId == card.Card.Id);
            Core2026Proof.Check(card.Card.Cost == member.Cost && card.Card.ATK == member.Attack && card.Card.Health == member.Health, "pinned output definition stats");
            if (card is Minion m) Core2026Proof.Check(m.Health == m.Card.Health && m.AttackDamage == m.Card.ATK +
                (m.Card.Id == "JAIL_311" && m.Controller.DeckZone.Count >= 25 ? 5 : 0), "generated minion effective stats");
        }
    }

    // Real admitted members only; retaining a Past card allows an empty Standard fixture.
    internal static void WithPools(Game g, string id, int count, Action action)
    {
        var installed = g.OfflinePools;
        var filters = OfflinePoolDeclarations.Cards.Single(c => c.CardId == id).Pools;
        var members = filters.SelectMany(f => OfflineCardPools.Candidates(g, g.Player1.HeroClass, f.Filter, Cards.FromId(id).AssetId)
            .Take(count).Select(c => c.Id)).ToHashSet();
        var xml = OfflinePoolFixtures.Catalog(); var rows = xml.Root.Element("Members");
        var retained = rows.Elements("Card").Where(c => members.Contains((string)c.Attribute("cardId"))).ToList();
        if (retained.Count == 0) retained.Add(rows.Elements("Card").First(c => (string)c.Attribute("scope") == "Past"));
        rows.ReplaceNodes(retained);
        var manifest = OfflinePoolFixtures.Manifest(xml);
        foreach (var f in filters) Core2026Proof.Check(OfflineCardPools.Candidates(manifest, g.Player1.HeroClass, f.Filter, Cards.FromId(id).AssetId).Count == count,
            "empty/singleton real pool fixture");
        var property = typeof(Game).GetProperty(nameof(Game.OfflinePools));
        try { property.SetValue(g, manifest); action(); }
        finally { property.SetValue(g, installed); }
    }

    internal static void CleanBoard(Game g, Controller p)
    {
        foreach (var m in p.BoardZone.ToArray()) { m.Silence(); Kill(g, m); }
        foreach (var location in p.BoardZone.GetLocations().ToArray()) location.Destroy();
        g.DeathProcessingAndAuraUpdate();
    }

    internal static void HandDeathrattle(Game g, Controller p, string id, params Func<Card, bool>[] matches)
    {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 0, "no Battlecry generation");
        PoolPilotProof.Resume(g, next => Kill(next, (Minion)next.IdEntityDic[source.Id]));
        Generated(g, source, p.HandZone.Take(1), 1);
        Generated(g, source, p.HandZone.Skip(1), 1, 1);
        Core2026Proof.Check(matches[0](p.HandZone[0].Card) && matches[1](p.HandZone[1].Card), "both output filters and creation order");
        PoolPilotProof.EmptyHand(p);
        WithPools(g, id, 0, () => {
            var m = Summon(p, id); var rng = g.Random.CaptureState(); Kill(g, m);
            Core2026Proof.Check(p.HandZone.Count == 0 && rng.Equals(g.Random.CaptureState()), "empty pools create nothing or RNG");
        });
        WithPools(g, id, 1, () => {
            var m = Summon(p, id); Kill(g, m);
            Generated(g, m, p.HandZone.Take(1), 1); Generated(g, m, p.HandZone.Skip(1), 1, 1);
        });
        PoolPilotProof.EmptyHand(p);
        foreach (int slots in new[] { 9, 10 })
        {
            PoolPilotProof.FillHand(p, slots); var m = Summon(p, id); long before = g.Random.DrawCount; Kill(g, m);
            Core2026Proof.Check(p.HandZone.Count == 10 && g.Random.DrawCount - before == 2, "partial/full hand samples both effects");
            PoolPilotProof.EmptyHand(p);
        }
        source = Summon(p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 0, "Silence removes both generators");
    }
}
