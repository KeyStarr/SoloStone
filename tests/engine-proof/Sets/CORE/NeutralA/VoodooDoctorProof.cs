using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class VoodooDoctorProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_011", (g, p, id) => {
        p.Hero.Damage = 3; Play(g, p, id, p.Hero); Core2026Proof.Check(p.Hero.Damage == 1, "restore two Health to friendly hero");
        Play(g, p, id, p.Hero); Core2026Proof.Check(p.Hero.Damage == 0 && p.Hero.Health == 30, "healing capped at maximum Health");
        var target = Summon(p.Opponent, "CS2_182"); target.Damage = 3; Play(g, p, id, target);
        Core2026Proof.Check(target.Damage == 1, "enemy minion may be healed");
        int health = p.Opponent.Hero.Health; Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Health == health, "full Health target does not overheal");
    }, legacyPoolMember: true);
}
