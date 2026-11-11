using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class OverlordRunthakProof
{
    internal static void Run() => StandardSetsProof.Case("CS3_025", (g, p, id) => {
        var source = (Minion)Play(g, p, id); var minion = (Minion)Add(p, "CS2_182"); var spell = Add(p, "CS2_029");
        var enemyHand = (Minion)Add(p.Opponent, "CS2_182"); var extraRunthak = Add(p, id);
        var ally = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(source.IsRush && source.AttackDamage == 3 && source.Health == 6, "printed 3/6 Rush");
        var clone = g.Clone(); Attack(clone, (Minion)clone.IdEntityDic[source.Id], (Minion)clone.IdEntityDic[enemy.Id]);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[minion.Id]).AttackDamage == 5 && minion.AttackDamage == 4, "cloned attack buffs cloned hand only");
        Attack(g, ally, p.Opponent.Hero); Core2026Proof.Check(minion.AttackDamage == 4, "other friendly attacker ignored");
        var legalAttack = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == source.Id && t.Target?.Id == enemy.Id);
        Core2026Proof.Check(g.Process(legalAttack), "printed Rush immediately attacks enemy minion");
        Core2026Proof.Check(minion.AttackDamage == 5 && minion.Health == 6 && extraRunthak[GameTag.ATK] == 4
            && spell[GameTag.ATK] == 0 && enemyHand.AttackDamage == 4 && ally.AttackDamage == 4,
            "attack buffs all own hand minions once; no spells, board or opponent hand");
        Core2026Proof.Check(minion.AppliedEnchantments.Any(e => e.Card.Id == "CS3_025e"), "exact Rallying Cry enchantment");
        End(g); Attack(g, enemy, p.Hero); End(g);
        Core2026Proof.Check(minion.AttackDamage == 5 && minion.Health == 6, "opponent attack ignored and permanent hand buff survives turn boundary");
        source.Silence(); g.DeathProcessingAndAuraUpdate(); Attack(g, source, p.Opponent.Hero);
        Core2026Proof.Check(minion.AttackDamage == 5, "Silence disables attack trigger; hand Runthak inactive");
        Kill(g, source); source = Summon(p, id); source.Damage = 5;
        enemy = Summon(p.Opponent, "CS2_182"); Attack(g, source, enemy);
        Core2026Proof.Check(source.Zone == p.GraveyardZone && minion.AttackDamage == 6 && minion.Health == 7,
            "attack declaration buffs hand before lethal retaliation");
        var attacker = Summon(p, "CS2_182"); Attack(g, attacker, p.Opponent.Hero);
        Core2026Proof.Check(minion.AttackDamage == 6, "dead source cannot buff later attacks");
        PlayEntity(g, p, minion); Core2026Proof.Check(minion.AttackDamage == 6 && minion.Health == 7, "hand buffs survive play");
        minion.Silence(); Core2026Proof.Check(minion.AttackDamage == 4 && minion.Health == 5, "Silence removes received permanent buffs");
    });
}
