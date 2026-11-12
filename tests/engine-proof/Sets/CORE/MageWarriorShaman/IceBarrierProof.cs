using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class IceBarrierProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_289", (g, p, id) => {
        Play(g, p, id); var defender = Summon(p, "CS2_182"); End(g);
        Play(g, p.Opponent, "CS2_029", p.Hero); Core2026Proof.Check(p.SecretZone.Count == 1 && p.Hero.Armor == 0, "spell damage is not an attack");
        var attacker = Summon(p.Opponent, "CS2_182"); Attack(g, attacker, defender);
        Core2026Proof.Check(p.SecretZone.Count == 1 && p.Hero.Armor == 0, "minion attack against minion does not trigger");
        attacker = Summon(p.Opponent, "CS2_182"); int attackerId = attacker.Id;
        Action<Game> attackOrCast = next => {
            Attack(next, (Minion)next.IdEntityDic[attackerId], next.Player1.Hero);
            Core2026Proof.Check(next.Player1.SecretZone.Count == 0 && next.Player1.Hero.Armor == 4 && next.Player1.Hero.Damage == 6, "eight Armor gained before four combat damage in original/clone/snapshot");
            var second = Summon(next.Player2, "CS2_182"); Attack(next, second, next.Player1.Hero);
            Core2026Proof.Check(next.Player1.Hero.Armor == 0 && next.Player1.SecretZone.Count == 0, "secret triggers once");
        };
        var clone = g.CloneExact(history: true); string before = Canonical.State(g);
        attackOrCast(clone); Core2026Proof.Check(before == Canonical.State(g), "clone leaves original state unchanged");
        attackOrCast(g); Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone), "same secret outcome in exact clone");
    }, legacyPoolMember: true);
}
