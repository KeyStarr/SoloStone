using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;
using static TimeTravelS2Proof;

internal static class BygoneDoomspeakerProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_008", (g, p, id) => {
        var own = Enumerable.Range(0, 3).Select(_ => Add(p, "CS2_182")).ToArray();
        var other = Enumerable.Range(0, 3).Select(_ => Add(p.Opponent, "CS2_171")).ToArray();
        long draws = g.Random.DrawCount;
        Resume(g, next => { Play(next, next.Player1, id); RewindTestHelpers.KeepIfOffered(next); });
        Core2026Proof.Check(p.HandZone.Count == 2 && p.Opponent.HandZone.Count == 2 && g.Random.DrawCount == draws + 2,
            "one seeded discard per player; played source excluded");
        Core2026Proof.Check(own.Count(c => c.Zone.Type == Zone.GRAVEYARD) == 1 && other.Count(c => c.Zone.Type == Zone.GRAVEYARD) == 1
            && p.DiscardedEntities.Count == 1 && p.Opponent.DiscardedEntities.Count == 1, "real discard bookkeeping on both owners");
        EmptyHand(p); EmptyHand(p.Opponent); draws = g.Random.DrawCount;
        Play(g, p, id); RewindTestHelpers.KeepIfOffered(g);
        Core2026Proof.Check(g.Random.DrawCount == draws && p.HandZone.IsEmpty && p.Opponent.HandZone.IsEmpty, "empty hands no RNG");
        var last = Add(p.Opponent, "CS2_182"); draws = g.Random.DrawCount;
        Play(g, p, id); RewindTestHelpers.KeepIfOffered(g);
        Core2026Proof.Check(last.Zone.Type == Zone.GRAVEYARD && p.Opponent.DiscardedEntities.Contains(last.Id)
            && g.Random.DrawCount == draws + 1, "singleton opposing hand discarded by its owner");
    });
}
