using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class PrimordialBearProof
{
    internal static void Run() => StandardSetsProof.Case("BE_855", (g, p, id) => {
        var source = (Minion)Add(p, id); source.AttackDamage = 5;
        PlayEntity(g, p, source); Core2026Proof.Check(p.Hero.AttackDamage == 5 && p.Hero.AppliedEnchantments.Any(e => e.Card.Id == "BE_855e"), "uses current buffed Attack and correct enchantment");
        Play(g, p, id); Core2026Proof.Check(p.Hero.AttackDamage == 7, "hero buffs stack");
        var clone = g.Clone(); End(clone); Core2026Proof.Check(clone.Player1.Hero.AttackDamage == 0 && p.Hero.AttackDamage == 7, "expiry clone isolated");
        Play(g, p, "BE_228", source); Kill(g, source); Core2026Proof.Check(p.Hero.AttackDamage == 7, "battlecry survives source silence and death");
        End(g); Core2026Proof.Check(p.Hero.AttackDamage == 0, "all temporary Attack expires at end turn");
        Core2026Proof.Check(p.Opponent.Hero.AttackDamage == 0, "opponent unaffected"); End(g);
        source = (Minion)Add(p, id); source.AttackDamage = 0; PlayEntity(g, p, source);
        Core2026Proof.Check(p.Hero.AttackDamage == 0, "zero Attack source grants zero"); End(g);
        Core2026Proof.Check(p.Hero.AttackDamage == 0, "zero buff expiry stays zero");
    });
}
