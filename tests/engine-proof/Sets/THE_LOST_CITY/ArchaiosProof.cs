using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ArchaiosProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_811", (g, p, id) => {
        var source = (Minion)Play(g, p, id); source.Damage = 2;
        var attacker = Summon(p, "CS2_182"); attacker.Damage = 3; var hand = Add(p, id);
        var clone = g.Clone(); Attack(clone, (Minion)clone.IdEntityDic[attacker.Id], clone.Player2.Hero);
        Core2026Proof.Check(attacker.Health == 2 && ((Minion)clone.IdEntityDic[attacker.Id]).Health == 4, "clone pre-attack Health set isolated");
        Attack(g, attacker, p.Opponent.Hero);
        Core2026Proof.Check(attacker.Health == 4 && attacker.BaseHealth == 4 && attacker.Damage == 0 && attacker.AppliedEnchantments.Any(e => e.Card.Id == "TLC_811e"), "attacker set to source current Health before combat");
        var defender = Summon(p.Opponent, "CS2_182"); attacker.IsExhausted = false; attacker.NumAttacksThisTurn = 0;
        Attack(g, attacker, defender);
        Core2026Proof.Check(attacker.Zone == p.GraveyardZone, "Health set before combat: four damage still kills attacker");
        Attack(g, source, p.Opponent.Hero); Core2026Proof.Check(source.Health == 4, "source attack does not set own Health");
        End(g); var enemy = Summon(p.Opponent, "CS2_182"); enemy.Damage = 3; Attack(g, enemy, p.Hero);
        Core2026Proof.Check(enemy.Health == 2, "opponent attack ignored"); End(g);
        source.Silence(); g.DeathProcessingAndAuraUpdate(); attacker = Summon(p, "CS2_182"); Attack(g, attacker, p.Opponent.Hero);
        Core2026Proof.Check(attacker.Health == 5, "Silence removes trigger; hand copy does not trigger");
        source = (Minion)Play(g, p, id); Kill(g, source); attacker = Summon(p, "CS2_182"); Attack(g, attacker, p.Opponent.Hero);
        Core2026Proof.Check(attacker.Health == 5, "dead source no longer triggers");
    });
}
