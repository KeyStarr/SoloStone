using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class EvasiveWyrmProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_DRG_079", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 5 && source.Health == 4 && source.IsRace(Race.DRAGON)
            && source.HasDivineShield && source.IsRush && source.CanAttack, "printed Dragon 5/4 shield and Rush readiness");
        Core2026Proof.Check(source.ValidAttackTargets.Contains(enemy) && !source.ValidAttackTargets.Contains(p.Opponent.Hero), "Rush targets minions only on play turn");
        Core2026Proof.Check(g.Process(MinionAttackTask.Any(p, source, enemy)), "natural Rush attack accepted");
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && source.Damage == 0 && !source.HasDivineShield, "shield absorbs retaliation and drops");
        source.TakeDamage(p.Opponent.Hero, 1);
        Core2026Proof.Check(source.Health == 3, "next hit damages unshielded Wyrm");
        Elusive(g, p, source);
    });
}
