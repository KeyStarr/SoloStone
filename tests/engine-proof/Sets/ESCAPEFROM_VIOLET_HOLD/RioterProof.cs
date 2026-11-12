using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class RioterProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_029", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        var deathrattleBranch = g.CloneExact(history: true);
        var doomedRioter = (Minion)deathrattleBranch.IdEntityDic[source.Id];
        var sporegnasher = Summon(deathrattleBranch.Player2, "EDR_110"); Kill(deathrattleBranch, sporegnasher);
        Core2026Proof.Check(doomedRioter.Zone.Type == Zone.GRAVEYARD && doomedRioter.AttackDamage == 1,
            "Sporegnasher Poisonous Deathrattle earns no survivor Attack outside combat");
        var target = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        Play(g, p, "CS2_008", target); Core2026Proof.Check(target.AttackDamage == 5, "surviving friendly damage grants Attack");
        Play(g, p, "CS2_008", source); Core2026Proof.Check(source.AttackDamage == 2, "self also friendly");
        target.HasDivineShield = true; Play(g, p, "CS2_008", target); Core2026Proof.Check(target.AttackDamage == 5, "shield absorption does not trigger");
        Play(g, p, "CS2_008", enemy); Core2026Proof.Check(enemy.AttackDamage == 4, "opponent minion excluded");
        Play(g, p, "CS2_008", p.Hero); Core2026Proof.Check(p.Hero.AttackDamage == 0, "friendly hero excluded");
        var clone = g.Clone(); Play(clone, clone.Player1, "CS2_008", (Minion)clone.IdEntityDic[target.Id]);
        Core2026Proof.Check(((Minion)clone.IdEntityDic[target.Id]).AttackDamage == 6 && target.AttackDamage == 5, "trigger clone isolated");
        End(g); End(g); Core2026Proof.Check(target.AttackDamage == 5, "buff persists across turns");
        var doomed = Summon(p, "CS2_231"); Play(g, p, "CS2_008", doomed);
        Core2026Proof.Check(doomed.Zone.Type == Zone.GRAVEYARD && doomed.AttackDamage == 1, "lethal damage earns no buff");
        var poisoner = Summon(p.Opponent, "EX1_170"); var victim = Summon(p, "CS2_182");
        End(g); Attack(g, poisoner, victim); End(g);
        Core2026Proof.Check(victim.Zone.Type == Zone.GRAVEYARD && victim.AttackDamage == 4, "Poisonous damage does not count as surviving");
        victim = Summon(p, "CS2_182"); End(g); Play(g, p.Opponent, "CS2_082"); p.Opponent.Hero.Weapon.Poisonous = true;
        Core2026Proof.Check(g.Process(HeroAttackTask.Any(p.Opponent, victim)), "Poisonous weapon attack accepted"); End(g);
        Core2026Proof.Check(victim.Zone.Type == Zone.GRAVEYARD && victim.AttackDamage == 4, "Poisonous weapon damage does not count as surviving");
        source.Silence(); Play(g, p, "CS2_008", target); Core2026Proof.Check(target.AttackDamage == 5, "Silence removes trigger");
        Kill(g, source); var departing = Summon(p, id); Kill(g, departing);
        var witness = Summon(p, "CS2_182"); Play(g, p, "CS2_008", witness);
        Core2026Proof.Check(witness.AttackDamage == 4, "unsilenced dead source cannot trigger");
    });
}
