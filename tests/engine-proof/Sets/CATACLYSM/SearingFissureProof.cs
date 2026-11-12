using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SearingFissureProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_582", (g, p, id) => {
        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        Summon(p, "CS2_052"); // Spell Damage +1.
        Play(g, p, id);
        Core2026Proof.Check(friendly.Damage == 2 && enemy.Damage == 2 && p.Hero.AttackDamage == 3 && p.Opponent.Hero.Damage == 0, "spell damage hits both boards, hero gains three Attack");
        var clone = g.CloneExact(history: true); End(clone);
        Core2026Proof.Check(clone.Player1.Hero.AttackDamage == 0 && p.Hero.AttackDamage == 3, "temporary Attack expires only in clone");
        End(g); Core2026Proof.Check(p.Hero.AttackDamage == 0, "Attack expires at end of turn");
        End(g); ClearBoard(g, p); ClearBoard(g, p.Opponent);
        Play(g, p, id); Core2026Proof.Check(p.Hero.AttackDamage == 3, "empty board still grants Attack");
        Play(g, p, id); Core2026Proof.Check(p.Hero.AttackDamage == 6, "temporary buffs stack");
        End(g); Core2026Proof.Check(p.Hero.AttackDamage == 0, "all stacked buffs expire");
    });
}
