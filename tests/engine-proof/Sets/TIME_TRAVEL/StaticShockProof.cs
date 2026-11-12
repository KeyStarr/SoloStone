using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class StaticShockProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_218", (g, p, id) => {
        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        TargetRules(g, p, id, enemy, friendly, false);
        PlayEntity(g, p, p.HandZone.Single(), enemy);
        Core2026Proof.Check(enemy.Damage == 1 && p.Hero.AttackDamage == 1 && p.Hero.AppliedEnchantments.Single().Card.Id == "TIME_218e", "one damage and exact hero attack enchantment");
        Summon(p, "TIME_856"); Play(g, p, id, friendly);
        Core2026Proof.Check(friendly.Damage == 3 && p.Hero.AttackDamage == 2, "spell power affects damage, hero attack stacks");
        var clone = g.CloneExact(history: true); End(clone);
        Core2026Proof.Check(clone.Player1.Hero.AttackDamage == 0 && p.Hero.AttackDamage == 2, "clone expiry isolated");
        End(g); Core2026Proof.Check(p.Hero.AttackDamage == 0, "attack expires at end of turn");
    });
}
