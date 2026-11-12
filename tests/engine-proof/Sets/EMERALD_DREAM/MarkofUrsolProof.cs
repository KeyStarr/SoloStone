using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static EmeraldDreamBatch3Proof;

internal static class MarkofUrsolProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_252", (g, p, id) => {
        var spell = Add(p, id);
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id), "no legal play without a minion");
        var friendly = Summon(p, "CS2_182"); friendly.Damage = 3;
        PlayEntity(g, p, spell, friendly);
        Core2026Proof.Check(friendly.AttackDamage == 3 && friendly.Health == 3 && friendly.Damage == 0
            && friendly.AppliedEnchantments.Single().Card.Id == "EDR_252e1", "friendly damaged minion set to 3/3 with exact enchantment");
        var enemy = Summon(p.Opponent, "CS2_182"); enemy.Damage = 2;
        Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.AttackDamage == 1 && enemy.Health == 1 && enemy.Damage == 0
            && enemy.AppliedEnchantments.Single().Card.Id == "EDR_252e", "enemy damaged minion set to 1/1 with exact enchantment");
        TargetRules(g, p, id, enemy, friendly, false);
        friendly.Silence(); enemy.Silence();
        Core2026Proof.Check(friendly.AttackDamage == 4 && friendly.Health == 5 && enemy.AttackDamage == 4 && enemy.Health == 5,
            "Silence restores printed stats");
        End(g); End(g);
        Core2026Proof.Check(friendly.Health == 5, "no temporary state leaked");
    });
}
