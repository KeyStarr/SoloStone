using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

internal static class CarrierWhelpProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_556", (g, p, id) => {
        var source = Summon(p, id);
        CataclysmPoolProof.WithPool(g, id, 0, () => {
            int entities = g.IdEntityDic.Count; var rng = g.Random.CaptureState();
            Resolve(g, source);
            Core2026Proof.Check(p.HandZone.IsEmpty && entities == g.IdEntityDic.Count && rng.Equals(g.Random.CaptureState()), "empty Dragon pool has no effect/RNG");
        });
        CataclysmPoolProof.WithPool(g, id, 1, () => {
            Resolve(g, source);
            CataclysmPoolProof.Generated(g, id, source, p.HandZone, 1, Zone.HAND);
            Core2026Proof.Check(p.HandZone[0].Card.IsRace(Race.DRAGON) && p.HandZone[0].Card.Cost <= 3, "random Dragon costs at most three");
        });
        EmptyHand(p);
        Resume(g, next => Play(next, next.Player1, id));
        source = p.BoardZone.Single(m => m.Id != source.Id);
        CataclysmPoolProof.Generated(g, id, source, p.HandZone, 1, Zone.HAND);
        Core2026Proof.Check(p.HandZone[0].Card.IsRace(Race.DRAGON) && p.HandZone[0].Card.Cost <= 3, "ordinary Battlecry generates filtered Dragon");
        EmptyHand(p); FillHand(p, 9); Resolve(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10, "last hand slot receives Dragon");
        var before = g.Random.DrawCount; Resolve(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && g.Random.DrawCount - before == 1, "full hand burns pick without overflow");
        EmptyHand(p);
        var enemy = Summon(p.Opponent, id); Resolve(g, enemy);
        Core2026Proof.Check(p.HandZone.IsEmpty && p.Opponent.HandZone.Count == 1, "opponent Battlecry uses its own hand");
    });
}
