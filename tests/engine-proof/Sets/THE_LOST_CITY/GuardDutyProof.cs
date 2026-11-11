using SabberStoneCore.Enums;
using static Core2026Proof;

internal static class GuardDutyProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_433", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var source = Add(p, id);
        PoolPilotProof.Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[source.Id]));
        var output = p.BoardZone.Where(m => m.Id != left.Id).ToArray();
        Core2026Proof.Check(output.Length == 3 && output.Select(m => m.Card.Cost).SequenceEqual(new[] { 6, 4, 2 }) && output.All(m => m.HasTaunt), "three Taunts in descending cost order");
        for (int i = 0; i < 3; i++) { Batch9PoolProof.Generated(g, source, output.Skip(i).Take(1), 1, i); Core2026Proof.Check(output[i].ZonePosition == i + 1, "spell appends in creation order"); }
        Batch9PoolProof.CleanBoard(g, p);
        Batch9PoolProof.WithPools(g, id, 0, () => {
            var rng = g.Random.CaptureState(); Play(g, p, id);
            Core2026Proof.Check(p.BoardZone.SlotCount == 0 && rng.Equals(g.Random.CaptureState()), "empty pools produce no summons or RNG");
        });
        Batch9PoolProof.WithPools(g, id, 1, () => {
            source = Play(g, p, id);
            for (int i = 0; i < 3; i++) Batch9PoolProof.Generated(g, source, p.BoardZone.Skip(i).Take(1), 1, i);
        });
        Batch9PoolProof.CleanBoard(g, p); LostCityPoolProof.FullBoard(p);
        long before = g.Random.DrawCount; Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && g.Random.DrawCount - before == 3, "full mixed board still samples all pools");
        Kill(g, p.BoardZone.Last()); before = g.Random.DrawCount; source = Play(g, p, id);
        output = p.BoardZone.Where(m => m[GameTag.DISPLAYED_CREATOR] == source.Id).ToArray();
        Core2026Proof.Check(output.Length == 1 && output[0].Card.Cost == 6 && output[0].HasTaunt && p.BoardZone.SlotCount == 7 && g.Random.DrawCount - before == 3, "one physical slot gets first summon only");
    });
}
