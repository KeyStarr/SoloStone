using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class HandOfAdalProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_BT_292", (g, p, id) => {
        var friendly = Summon(p, "CS2_182"); friendly.Damage = 2;
        Play(g, p, id, friendly);
        Core2026Proof.Check(friendly.AttackDamage == 6 && friendly.BaseHealth == 6 && friendly.Damage == 2 && p.HandZone.Count == 1, "+2/+1 preserves damage; draws one");
        friendly.Silence(); Core2026Proof.Check(friendly.AttackDamage == 4 && friendly.BaseHealth == 5 && friendly.Damage == 1, "Silence removes Health buff while retaining current Health");
        var enemy = Summon(p.Opponent, "CS2_182"); Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.AttackDamage == 6 && enemy.Health == 6 && p.HandZone.Count == 2, "enemy target allowed; caster draws");
        TargetRules(g, p, id, enemy, friendly, false);
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        int damage = p.Hero.Damage; Play(g, p, id, friendly);
        Core2026Proof.Check(friendly.AttackDamage == 6 && p.Hero.Damage == damage + 1, "empty deck still buffs then fatigues");
        End(g); End(g); Core2026Proof.Check(friendly.AttackDamage == 6, "permanent buff survives turn end");
    });
}
