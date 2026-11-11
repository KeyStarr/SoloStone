using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using static Core2026Proof;

internal static class MisterClocksworthProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_038", (g, p, id) => {
        var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        var source = StandardSetsProof.PlayAt(g, p, id, 2);
        ChronoDaggersProof.Keep(g);
        var summoned = p.BoardZone.Slots.Skip(3).TakeWhile(e => e.Id != right.Id).ToArray();
        var pool = OfflineCardPools.Candidates(g, p.HeroClass, new OfflinePoolFilter(PoolScope.Standard, PoolClassMode.Any,
            type: CardType.MINION, rarity: Rarity.LEGENDARY), source.Card.AssetId).Select(c => c.Id).ToHashSet();
        Core2026Proof.Check(p.BoardZone.Slots.Select(e => e.Id).SequenceEqual(new[] { location.Id, left.Id, source.Id }
            .Concat(summoned.Select(e => e.Id)).Append(right.Id)) &&
            summoned.Length == (summoned[0].Card.Id == "CORE_DAL_575" ? 3 : 2) &&
            summoned.All(e => e.Card.Rarity == Rarity.LEGENDARY && pool.Contains(e.Card.Id)),
            "two admitted Standard Legendary minions immediately right of physical source slot: " +
            string.Join(",", p.BoardZone.Slots.Select(e => $"{e.Id}:{e.Card.Id}/{e.Card.Rarity}/{pool.Contains(e.Card.Id)}")));
        foreach (string variant in new[] { "TIME_038t1", "TIME_038t2", "TIME_038t3" })
            Core2026Proof.Check(Cards.FromId(variant).Implemented && !Cards.FromId(variant).Collectible && Cards.FromId(variant).Cost == 8,
                "registered noncollectible Clocksworth variant " + variant);
    });
}
