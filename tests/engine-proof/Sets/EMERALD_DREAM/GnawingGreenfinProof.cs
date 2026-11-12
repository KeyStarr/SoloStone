using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static EmeraldDreamPoolProof;

internal static class GnawingGreenfinProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_999", (g, p, id) => {
        var source = Summon(p, id);
        RandomHandBoundaries(g, p, id, () => PoolPilotProof.Resolve(g, source));
        Generated(g, source, p.HandZone, 0);
        Kill(g, source);
        source = (Minion)Add(p, id);
        PoolPilotProof.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[source.Id]));
        Generated(g, source, p.HandZone, 1);
        Core2026Proof.Check(p.HandZone[0].ZonePosition == 0, "generated Murloc first in hand");
        PoolPilotProof.EmptyHand(p); ClearBoard(g, p);
        source = Summon(p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 0, "summoning, Silence and death do not repeat battlecry");
    });
}
