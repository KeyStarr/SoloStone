using SabberStoneCore.Enums;
using static Core2026Proof;
using static StandardSetsProof;
using static CataclysmPoolProof;

internal static class CeremonialClashProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_569", (g, p, id) => {
        var location = LocationProofs.Location(p);
        var source = Add(p, id);
        PoolPilotProof.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[source.Id]));
        Generated(g, id, source, p.BoardZone, 3, Zone.PLAY);
        Core2026Proof.Check(p.BoardZone.Select(m => m.Card.Cost).SequenceEqual(new[] { 3, 2, 1 })
            && p.BoardZone.GetSlot(0) == location && p.OverloadOwed == 1, "3/2/1 creation order; location preserved; Overload one");
        PutBoardAside(p);
        WithPool(g, id, 0, () => {
            long before = g.Random.DrawCount; Play(g, p, id);
            Core2026Proof.Check(p.BoardZone.IsEmpty && g.Random.DrawCount == before && p.OverloadOwed == 2, "empty pools retain Overload, no RNG");
        });
        foreach (int cost in new[] { 3, 2, 1 }) WithPool(g, id, 1, () => {
            source = Play(g, p, id);
            Generated(g, id, source, p.BoardZone, 1, Zone.PLAY);
            Core2026Proof.Check(p.BoardZone.Single().Card.Cost == cost, "only nonempty cost pool summons");
            PutBoardAside(p);
        }, cost);
        location = LocationProofs.Location(p);
        for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        long rng = g.Random.DrawCount;
        source = Play(g, p, id);
        var generated = p.BoardZone.Where(m => m[GameTag.DISPLAYED_CREATOR] == source.Id).ToArray();
        Generated(g, id, source, generated, 1, Zone.PLAY);
        Core2026Proof.Check(generated.Single().Card.Cost == 3 && generated.Single().ZonePosition == 6
            && p.BoardZone.IsFull && g.Random.DrawCount - rng == 3, "first pick takes last physical slot; all pools sampled");
        rng = g.Random.DrawCount; Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && g.Random.DrawCount - rng == 3, "full board prevents every summon");
    });
}
