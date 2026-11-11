using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class AcolyteOfPainProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_007", (g, p, id) => {
        var source = (Minion)Play(g, p, id); var otherMinion = Summon(p.Opponent, "CS2_182");
        Play(g, p, "CS2_189", otherMinion); Core2026Proof.Check(p.HandZone.Count == 0, "other minion damage does not draw");
        Play(g, p, "CS2_189", source); Core2026Proof.Check(p.HandZone.Count == 1 && source.Health == 3, "self damage draws one");
        End(g); Play(g, p.Opponent, "CS2_189", source); Core2026Proof.Check(p.HandZone.Count == 2 && p.Opponent.HandZone.Count == 1, "opponent damage draws for source owner only");
        End(g); int beforeLethal = p.HandZone.Count; PoolPilotProof.Resume(g, next => {
            Play(next, next.Player1, "CS2_029", (Minion)next.IdEntityDic[source.Id]);
            Core2026Proof.Check(next.Player1.HandZone.Count == beforeLethal + 1 && next.IdEntityDic[source.Id].Zone.Type == Zone.GRAVEYARD, "lethal damage still draws in original/clone/snapshot");
    });
        ParkBoard(p); source = (Minion)Play(g, p, id); source.Silence(); int hand = p.HandZone.Count;
        Play(g, p, "CS2_189", source); Core2026Proof.Check(p.HandZone.Count == hand, "Silence removes damage trigger");
        Kill(g, source); Play(g, p, "CS2_189", otherMinion); Core2026Proof.Check(p.HandZone.Count == hand, "departed source has no damage listener");
    }, legacyPoolMember: true);
}
