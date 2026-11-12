using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class FortifyProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_620", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_200"); var friend = Summon(p, "CS2_182");
        enemy.Health = 20; TargetRules(g, p, id, enemy, friend, true);
        Play(g, p, id, enemy); Core2026Proof.Check(p.Hero.Armor == 3 && enemy.Damage == 3, "gain Armor before counting damage");
        Summon(p, "EX1_012"); Play(g, p, id, enemy);
        Core2026Proof.Check(p.Hero.Armor == 6 && enemy.Damage == 10, "current total Armor; Spell Damage applies to Armor-based damage");
        enemy.HasDivineShield = true; Play(g, p, id, enemy);
        Core2026Proof.Check(p.Hero.Armor == 9 && enemy.Damage == 10 && !enemy.HasDivineShield, "shield absorbs damage but Armor remains");
    });
}
