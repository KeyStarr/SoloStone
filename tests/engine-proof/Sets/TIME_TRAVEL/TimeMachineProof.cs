using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using static Core2026Proof;

internal static class TimeMachineProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_035", (g, p, id) => {
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, RewindSliceProof.Rewind, 120506);
        var machine = Summon(p, id); Core2026Proof.Check(machine.HasTaunt, "pinned Taunt");
        var rng = g.Random.CaptureState(); Kill(g, machine);
        Core2026Proof.Check(p.HandZone.Count == (candidates.Count == 0 ? 0 : 1) && p.HandZone.All(c => c.Card[(GameTag)3934] == 1),
            "installed admitted pool: Rewind output or empty no-op");
        if (candidates.Count == 0) Core2026Proof.Check(rng.Equals(g.Random.CaptureState()), "empty Rewind pool consumes no RNG");
        PoolPilotProof.EmptyHand(p);
        RewindSliceProof.WithRewindPool(g, () => {
            var pool = OfflineCardPools.Candidates(g, p.HeroClass, RewindSliceProof.Rewind, 120506);
            Core2026Proof.Check(pool.Select(c => c.Id).Order().SequenceEqual(new[] { "TIME_002", "TIME_003", "TIME_004" }),
                "own keyword only: Morchie and Time Machine referenced Rewind excluded");
            while (!p.BoardZone.IsFull) Summon(p, id);
            Kill(g, p.BoardZone[0]);
            Core2026Proof.Check(p.HandZone.Count == 1 && pool.Any(c => c.Id == p.HandZone[0].Card.Id), "Deathrattle grants one on full board");
            PoolPilotProof.FillHand(p); Kill(g, p.BoardZone[0]);
            Core2026Proof.Check(p.HandZone.Count == 10, "full hand cannot retain generated card");
        });
        RewindSliceProof.CheckCopies(g, next => Kill(next, next.Player1.BoardZone[0]));
    });
}
