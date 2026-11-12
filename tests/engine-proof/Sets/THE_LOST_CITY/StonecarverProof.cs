using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class StonecarverProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_623", (g, p, id) => {
        var healthy = Summon(p, "CS2_182"); var damaged = Summon(p, "CS2_182"); damaged.Damage = 2;
        var enemy = Summon(p.Opponent, "CS2_182"); enemy.Damage = 1;
        var source = (Minion)Play(g, p, id); source.Damage = 1; var hand = Add(p, id);
        var clone = g.Clone(); End(clone);
        Core2026Proof.Check(damaged.AttackDamage == 4 && ((Minion)clone.IdEntityDic[damaged.Id]).AttackDamage == 6, "clone buff isolated");
        End(g); Core2026Proof.Check(damaged.AttackDamage == 6 && damaged.Health == 5 && damaged.Damage == 2, "only other damaged friendly minion gets +2/+2; keeps damage");
        Core2026Proof.Check(healthy.AttackDamage == 4 && enemy.AttackDamage == 4 && source.AttackDamage == 1 && ((Minion)hand).AttackDamage == 1, "healthy, enemy, self and hand excluded");
        End(g); Core2026Proof.Check(damaged.AttackDamage == 6, "opponent end does not trigger");
        source.Silence(); g.DeathProcessingAndAuraUpdate(); End(g); End(g);
        Core2026Proof.Check(damaged.AttackDamage == 6, "Silence stops trigger");
        source = (Minion)Play(g, p, id); Kill(g, source); End(g); End(g);
        Core2026Proof.Check(damaged.AttackDamage == 6, "source leaving stops trigger");
        ClearBoard(g, p); source = (Minion)Play(g, p, id); source.Damage = 1; End(g); End(g);
        Core2026Proof.Check(source.AttackDamage == 1, "no eligible target never buffs self");
        var first = Summon(p, "CS2_182"); var second = Summon(p, "CS2_182"); first.Damage = second.Damage = 1;
        End(g); End(g); Core2026Proof.Check(new[] {first.AttackDamage, second.AttackDamage}.Order().SequenceEqual(new[] {4,6}), "randomly selects exactly one eligible minion");
    });
}
