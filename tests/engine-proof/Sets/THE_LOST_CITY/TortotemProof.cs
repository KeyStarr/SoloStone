using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class TortotemProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_412", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsRace(Race.TOTEM) && p.HandZone.Count == 0, "Totem; no immediate generation");
        PoolPilotProof.Resume(g, End);
        Batch9PoolProof.Generated(g, source, p.HandZone, 1);
        Core2026Proof.Check(p.HandZone[0].Card.Races.Count > 1 || p.HandZone[0].Card.IsRace(Race.ALL), "multiple types including Amalgams");
        int count = p.HandZone.Count; End(g);
        Core2026Proof.Check(p.HandZone.Count == count + 1, "opponent end only normal friendly turn draw");
        PoolPilotProof.EmptyHand(p);
        Batch9PoolProof.WithPools(g, id, 0, () => {
            var rng = g.Random.CaptureState(); End(g);
            Core2026Proof.Check(p.HandZone.Count == 0 && rng.Equals(g.Random.CaptureState()), "empty end-turn pool consumes no RNG");
            End(g); PoolPilotProof.EmptyHand(p);
        });
        Batch9PoolProof.WithPools(g, id, 1, () => {
            End(g); Batch9PoolProof.Generated(g, source, p.HandZone, 1);
            End(g); PoolPilotProof.EmptyHand(p);
        });
        PoolPilotProof.FillHand(p); long before = g.Random.DrawCount; End(g);
        Core2026Proof.Check(p.HandZone.Count == 10 && g.Random.DrawCount - before == 1, "full hand still picks once");
        End(g); PoolPilotProof.EmptyHand(p);
        source.Silence(); End(g); Core2026Proof.Check(p.HandZone.Count == 0, "Silence removes turn trigger");
        End(g); PoolPilotProof.EmptyHand(p); Kill(g, source);
        source = Summon(p, id); Kill(g, source); End(g);
        Core2026Proof.Check(p.HandZone.Count == 0, "source leaving play removes trigger");
        End(g); PoolPilotProof.EmptyHand(p);
        source = (Minion)Add(p, id); End(g); End(g); End(g);
        Core2026Proof.Check(p.HandZone.All(c => c[GameTag.DISPLAYED_CREATOR] != source.Id), "hand source does not trigger");
    });
}
