using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class RaptorNestNurseProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_434", (g, p, id) => {
        var source = (Minion)Add(p, id);
        PoolPilotProof.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[source.Id]));
        Batch9PoolProof.Generated(g, source, p.HandZone, 1);
        Core2026Proof.Check(p.HandZone[0].Card.Type == CardType.MINION && p.HandZone[0].Card.Cost == 1, "Battlecry one-cost minion");
        PoolPilotProof.EmptyHand(p);
        PoolPilotProof.Resume(g, next => Kill(next, (Minion)next.IdEntityDic[source.Id]));
        Batch9PoolProof.Generated(g, source, p.HandZone, 1, 1);
        Core2026Proof.Check(p.HandZone[0].Card.Type == CardType.SPELL && p.HandZone[0].Card.Cost == 1, "Deathrattle one-cost spell");
        PoolPilotProof.EmptyHand(p);
        Batch9PoolProof.WithPools(g, id, 0, () => {
            var rng = g.Random.CaptureState(); var m = (Minion)Play(g, p, id); Kill(g, m);
            Core2026Proof.Check(p.HandZone.Count == 0 && rng.Equals(g.Random.CaptureState()), "empty both pools");
        });
        Batch9PoolProof.WithPools(g, id, 1, () => {
            var m = (Minion)Play(g, p, id); Batch9PoolProof.Generated(g, m, p.HandZone, 1);
            PoolPilotProof.EmptyHand(p); Kill(g, m); Batch9PoolProof.Generated(g, m, p.HandZone, 1, 1);
        });
        PoolPilotProof.EmptyHand(p);
        source = (Minion)Play(g, p, id); PoolPilotProof.EmptyHand(p); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 0, "Silence removes Deathrattle");
        PoolPilotProof.FillHand(p, 9); source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 10, "Battlecry fills last slot");
        long before = g.Random.DrawCount; Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && g.Random.DrawCount - before == 1, "full-hand Deathrattle burns result after pick");
        PoolPilotProof.EmptyHand(p); PoolPilotProof.FillHand(p, 9);
        source = (Minion)Summon(p, id); Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10, "Deathrattle fills last slot");
        PoolPilotProof.EmptyHand(p);
        source = (Minion)Add(p, id); PoolPilotProof.FillHand(p);
        before = g.Random.DrawCount; PoolPilotProof.Resolve(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && g.Random.DrawCount - before == 1, "copied Battlecry respects full hand");
    });
}
