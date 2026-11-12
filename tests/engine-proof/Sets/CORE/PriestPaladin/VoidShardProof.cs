using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

internal static class VoidShardProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_SW_442", (g, p, id) => {
        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); enemy.BaseHealth = 20;
        var spell = Add(p, id);
        var targets = p.Options().Where(t => t.Source?.Id == spell.Id).Select(t => t.Target?.Id).ToHashSet();
        Core2026Proof.Check(new[] { friendly.Id, enemy.Id, p.Hero.Id, p.Opponent.Hero.Id }.All(entity => targets.Contains(entity)), "any character target allowed");
        p.Hero.Damage = 16; p.Opponent.Hero.Armor = 7;
        Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[spell.Id], next.Player2.Hero));
        Core2026Proof.Check(p.Hero.Damage == 12 && p.Opponent.Hero.Armor == 3 && p.Opponent.Hero.Damage == 0,
            "Lean_LifestealArmorDamage: four Armor damage heals four");
        Summon(p, "CS2_052"); Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Hero.Damage == 7 && p.Opponent.Hero.Armor == 0 && p.Opponent.Hero.Damage == 2,
            "Lean_LifestealArmorDamage: Spell Damage makes five; mixed three Armor plus two Health heals five");
        enemy.HasDivineShield = true; Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Damage == 0 && !enemy.HasDivineShield && p.Hero.Damage == 7, "shield prevents damage and healing");
        Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Damage == 5 && p.Hero.Damage == 2, "printed targeted effect heals normal minion damage");
    });
}
