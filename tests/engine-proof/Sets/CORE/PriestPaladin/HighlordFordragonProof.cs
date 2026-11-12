using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class HighlordFordragonProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_SW_047", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.HasDivineShield, "printed Divine Shield");
        var spell = Add(p, "CS2_004");
        var recipient = (Minion)Add(p, "CS2_182");
        source.TakeDamage(p.Opponent.Hero, 1); g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(!source.HasDivineShield && source.Damage == 0 && recipient.AttackDamage == 9 && recipient.Health == 10 && spell.Cost == 1, "self shield loss buffs only hand minion +5/+5");
        var friendly = Summon(p, "EX1_008"); friendly.HasDivineShield = true;
        var enemy = Summon(p.Opponent, "EX1_008"); enemy.HasDivineShield = true;
        enemy.TakeDamage(p.Hero, 1); g.ProcessTasks();
        Core2026Proof.Check(recipient.AttackDamage == 9, "opponent minion shield loss excluded");
        p.Hero.HasDivineShield = true; p.Hero.TakeDamage(p.Opponent.Hero, 1); g.ProcessTasks();
        Core2026Proof.Check(recipient.AttackDamage == 9 && !p.Hero.HasDivineShield, "hero shield loss excluded");
        friendly.TakeDamage(p.Opponent.Hero, 1); g.ProcessTasks();
        Core2026Proof.Check(recipient.AttackDamage == 14 && recipient.Health == 15, "other friendly shield loss triggers");
        friendly.TakeDamage(p.Opponent.Hero, 1); g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(recipient.AttackDamage == 14, "ordinary damage cannot trigger");
        var second = (Minion)Add(p, "CS2_182");
        source.HasDivineShield = true; var clone = g.CloneExact(history: true);
        var cloneSource = (Minion)clone.IdEntityDic[source.Id]; cloneSource.TakeDamage(clone.Player2.Hero, 1); clone.ProcessTasks();
        Core2026Proof.Check(source.HasDivineShield && recipient.AttackDamage == 14 && second.AttackDamage == 4, "clone selection leaves original state unchanged");
        source.TakeDamage(p.Opponent.Hero, 1); g.ProcessTasks();
        Core2026Proof.Check(recipient.AttackDamage + second.AttackDamage == 23 && HsSim.Canonical.State(g) == HsSim.Canonical.State(clone), "one random hand minion; seeded clone parity");
        End(g); End(g); Core2026Proof.Check(recipient.AttackDamage + second.AttackDamage == 23, "buffs permanent; source trigger persists");
        PlayEntity(g, p, recipient); recipient.Silence();
        Core2026Proof.Check(recipient.AttackDamage == 4 && recipient.Health == 5, "hand buff carries into play and Silence removes it");
        int beforeSilence = second.AttackDamage;
        int healthBeforeSilence = second.Health;
        source.HasDivineShield = true;
        Core2026Proof.Check(source.HasDivineShield && !source.IsSilenced, "Fordragon has shield and active ability before Silence");
        source.Silence(); g.ProcessTasks();
        Core2026Proof.Check(source.IsSilenced && !source.HasDivineShield && second.AttackDamage == beforeSilence && second.Health == healthBeforeSilence,
            "Silence with shield intact cannot resolve its queued hand buff");
        source.HasDivineShield = true;
        source.TakeDamage(p.Opponent.Hero, 1); g.ProcessTasks();
        int attack = second.AttackDamage;
        Core2026Proof.Check(source.ActivatedTrigger == null && attack == beforeSilence, "Silence removes shield-loss trigger");
        Kill(g, source); source = (Minion)Play(g, p, id); Kill(g, source);
        var shield = Summon(p, "EX1_008"); shield.HasDivineShield = true;
        shield.TakeDamage(p.Opponent.Hero, 1); g.ProcessTasks();
        Core2026Proof.Check(second.AttackDamage == attack, "source leaving play prevents trigger");
        source = (Minion)Add(p, id); shield.HasDivineShield = true;
        shield.TakeDamage(p.Opponent.Hero, 1); g.ProcessTasks();
        Core2026Proof.Check(source.AttackDamage == 5 && second.AttackDamage == attack, "hand source has no active trigger");
        foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
        source = (Minion)Play(g, p, id); Add(p, "CS2_004");
        source.TakeDamage(p.Opponent.Hero, 1); g.ProcessTasks();
        Core2026Proof.Check(p.HandZone.Count == 1 && p.HandZone[0].Card.Id == "CS2_004", "no hand minions harmless");
    });
}
