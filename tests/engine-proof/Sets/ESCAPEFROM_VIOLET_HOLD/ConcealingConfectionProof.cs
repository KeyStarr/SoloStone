using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static PoolPilotProof;
using static VioletHoldPoolProof;

internal static class ConcealingConfectionProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_460", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsRace(Race.MECHANICAL) && p.HandZone.Count == 0, "Mech and no Battlecry generation");
        var expected = OfflineCardPools.Pick(g.CloneExact(), VioletHoldPoolProof.Filter(id), p.HeroClass, source.Card.AssetId);
        Resume(g, next => Kill(next, (Minion)next.IdEntityDic[source.Id]));
        var result = p.HandZone.Single();
        Member(g, source, result, VioletHoldPoolProof.Filter(id));
        Core2026Proof.Check(result.Card.Id == expected.Id && result.Zone == p.HandZone && result.ZonePosition == 0, "death generates deterministic weapon in hand");
        EmptyHand(p);
        source = Summon(p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 0, "silenced Deathrattle removed");
        source = Summon(p, id);
        SabberStoneCore.Actions.Generic.ReturnToHandBlock(p, source);
        End(g); End(g);
        Core2026Proof.Check(p.HandZone.Count == 2 && p.HandZone.All(c => c.Card.Id != id || c.Id == source.Id), "bounce and turn changes do not fire Deathrattle");
        EmptyHand(p);
        int enemyHand = p.Opponent.HandZone.Count;
        var enemy = Summon(p.Opponent, id); Kill(g, enemy);
        Core2026Proof.Check(p.HandZone.Count == 0 && p.Opponent.HandZone.Count == enemyHand + 1, "enemy Deathrattle belongs to enemy");
        Member(g, enemy, p.Opponent.HandZone.Last(), VioletHoldPoolProof.Filter(id));
        foreach (int count in new[] { 0, 1 }) WithPool(g, id, count, () => {
            EmptyHand(p); var minion = Summon(p, id); var rng = g.Random.DrawCount;
            Kill(g, minion);
            Core2026Proof.Check(p.HandZone.Count == count && g.Random.DrawCount - rng == count, "empty/singleton weapon pool and RNG");
            if (count == 1) Member(g, minion, p.HandZone.Single(), VioletHoldPoolProof.Filter(id));
        });
        EmptyHand(p); FillHand(p, 9); source = Summon(p, id); Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10, "last hand slot filled");
        source = Summon(p, id);
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        int before = p.GraveyardZone.Count; var draws = g.Random.DrawCount;
        Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.GraveyardZone.Count == before + 2 && g.Random.DrawCount == draws + 1,
            "full hand burns generated weapon while consuming one pick");
        Core2026Proof.Check(p.BoardZone.SlotCount == 6, "death on full board generates only the weapon in hand");
    });
}
