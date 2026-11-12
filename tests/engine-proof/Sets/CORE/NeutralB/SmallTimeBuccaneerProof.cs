using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SmallTimeBuccaneerProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_WON_351", (g, p, id) => {
        var hand = (Minion)Add(p, id);
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsRace(Race.PIRATE) && source.AttackDamage == 1 && source.Health == 2, "unarmed Pirate 1/2");
        End(g); Play(g, p.Opponent, "CS2_106"); End(g);
        Core2026Proof.Check(source.AttackDamage == 1, "opponent weapon does not enable aura");
        Play(g, p, "CS2_106");
        Core2026Proof.Check(source.AttackDamage == 3 && hand.AttackDamage == 1, "own weapon enables +2 only in play");
        var clone = g.Clone(); clone.Player1.Hero.Weapon.Destroy(); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(((Minion)clone.IdEntityDic[source.Id]).AttackDamage == 1 && source.AttackDamage == 3,
            "clone weapon destruction updates aura without touching original");
        End(g); End(g); Core2026Proof.Check(source.AttackDamage == 3, "buff persists across turns while weapon remains");
        p.Hero.Weapon.Destroy(); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(source.AttackDamage == 1, "losing weapon removes buff");
        Play(g, p, "CS2_106"); source.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(source.AttackDamage == 1 && source.IsRace(Race.PIRATE), "Silence removes aura, retains printed type");
        var removed = Summon(p, id); Core2026Proof.Check(removed.AttackDamage == 3, "summoned source activates aura");
        Kill(g, removed); p.Hero.Weapon.Destroy(); g.DeathProcessingAndAuraUpdate(); Play(g, p, "CS2_106");
        Core2026Proof.Check(removed.AttackDamage == 1 && source.AttackDamage == 1, "dead and silenced sources cannot regain aura");
    });
}
