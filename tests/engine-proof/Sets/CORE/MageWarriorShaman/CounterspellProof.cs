using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class CounterspellProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_287", (g, p, id) => {
        var secret = Play(g, p, id); Play(g, p, "CS2_029", p.Opponent.Hero);
        Core2026Proof.Check(p.SecretZone.Count == 1 && p.Opponent.Hero.Damage == 6, "own spell never triggers secret");
        End(g); Play(g, p.Opponent, "CS2_182"); Core2026Proof.Check(p.SecretZone.Count == 1, "enemy minion does not trigger secret");
        Action<Game> attackOrCast = next => {
            Play(next, next.Player2, "CS2_029", next.Player1.Hero);
            Core2026Proof.Check(next.Player1.Hero.Damage == 0 && next.Player1.SecretZone.Count == 0 && next.IdEntityDic[secret.Id].Zone.Type == Zone.GRAVEYARD, "enemy spell countered and secret consumed in original/clone/snapshot");
            Play(next, next.Player2, "CS2_029", next.Player1.Hero);
            Core2026Proof.Check(next.Player1.Hero.Damage == 6, "consumed secret no longer counters");
        };
        var clone = g.CloneExact(history: true); string before = Canonical.State(g);
        attackOrCast(clone); Core2026Proof.Check(before == Canonical.State(g), "clone leaves original state unchanged");
        attackOrCast(g); Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone), "same secret outcome in exact clone");
    }, legacyPoolMember: true);
}
