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

internal static class CeaseToExistProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_433", (g, p, id) => {
        long draws = g.Random.DrawCount; Play(g, p, id); RewindTestHelpers.KeepIfOffered(g);
        Core2026Proof.Check(g.Random.DrawCount == draws, "no enemy minion no RNG");
        var friendly = Summon(p, "CS2_182");
        var doomed = Summon(p.Opponent, "FP1_002");
        doomed.HasDivineShield = true;
        var location = LocationProofs.Location(p.Opponent);
        Play(g, p, id); RewindTestHelpers.KeepIfOffered(g);
        Core2026Proof.Check(doomed.IsSilenced && doomed.Zone.Type == Zone.GRAVEYARD && p.Opponent.BoardZone.Count == 0
            && p.BoardZone.Contains(friendly) && location.Zone.Type == Zone.PLAY, "silence before destroy suppresses Creeper Deathrattle, ignores Shield and locations");
        var targets = Enumerable.Range(0, 3).Select(_ => Summon(p.Opponent, "CS2_182")).ToArray();
        Resume(g, next => { Play(next, next.Player1, id); RewindTestHelpers.KeepIfOffered(next); });
        Core2026Proof.Check(targets.Count(m => m.IsSilenced && m.Zone.Type == Zone.GRAVEYARD) == 1, "one distinct random enemy minion");
        ParkBoard(p); ParkBoard(p.Opponent);
    });
}
