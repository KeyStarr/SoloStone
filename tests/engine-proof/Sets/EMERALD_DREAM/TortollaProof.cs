using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static EmeraldDreamBatch3Proof;

internal static class TortollaProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_471", (g, p, id) => {
        var source = (Minion)Play(g, p, id); var other = Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(source.HasTaunt && source.CantBeTargetedBySpells && source.CantBeTargetedByHeroPowers, "Taunt and Elusive");
        int armor = p.Hero.Armor;
        other.TakeDamage(p.Opponent.Hero, 1); g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.Hero.Armor == armor && source.AttackDamage == 1, "other minion damage ignored");
        source.TakeDamage(p.Opponent.Hero, 0); g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(source.AttackDamage == 1 && p.Hero.Armor == armor, "zero damage ignored");
        source.TakeDamage(p.Opponent.Hero, 2); g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(source.Health == 28 && source.AttackDamage == 2 && p.Hero.Armor == armor + 1, "one Armor and Attack per damage event");
        Core2026Proof.Check(source.AppliedEnchantments.Single().Card.Id == "EDR_471e", "exact Attack enchantment");
        var clone = g.Clone(); var copied = (Minion)clone.IdEntityDic[source.Id];
        copied.TakeDamage(clone.Player2.Hero, 1); clone.ProcessTasks(); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(copied.AttackDamage == 3 && source.AttackDamage == 2 && p.Hero.Armor == armor + 1, "clone damage trigger independent");
        End(g); Core2026Proof.Check(source.AttackDamage == 2, "Attack persists at end of turn");
        source.TakeDamage(p.Hero, 1); g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(source.AttackDamage == 3 && p.Hero.Armor == armor + 2, "self damage on opponent turn still triggers");
        source.Silence(); source.TakeDamage(p.Opponent.Hero, 1); g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(source.AttackDamage == 1 && p.Hero.Armor == armor + 2, "Silence removes trigger and Attack");
        Kill(g, source); var fresh = Summon(p, id); Kill(g, fresh);
        Core2026Proof.Check(fresh.ActivatedTrigger == null, "unsilenced source leaves play without trigger");
    });
}
