using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SelflessProtectorProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_208", (g, p, id) => {
        var source = (Minion)Play(g, p, id); var other = Summon(p, "CS2_182");
        Core2026Proof.Check(source.HasTaunt, "printed Taunt");
        var clone = g.CloneExact(history: true); var copy = (Minion)clone.IdEntityDic[source.Id]; copy.TakeDamage(clone.Player2.Hero, 1); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(copy.Damage == 2 && source.Damage == 0, "clone predamage isolated");
        source.TakeDamage(p.Hero, 1); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(source.Damage == 2, "friendly damage increased by one");
        other.TakeDamage(p.Opponent.Hero, 1); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(other.Damage == 1, "other minion damage unaffected");
        source.TakeHeal(p.Hero, 10); source.HasDivineShield = true;
        source.TakeDamage(p.Opponent.Hero, 1); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(source.Damage == 0 && !source.HasDivineShield, "Divine Shield absorbs entire hit");
        source.Silence(); g.DeathProcessingAndAuraUpdate(); source.TakeDamage(p.Opponent.Hero, 1); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(source.Damage == 1 && !source.HasTaunt, "Silence removes extra damage and Taunt");
        Kill(g, source); other.TakeDamage(p.Opponent.Hero, 1); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(other.Damage == 2, "departed source has no global effect");
        var combat = Summon(p, id); var attacker = Summon(p.Opponent, "CS2_182"); End(g); Attack(g, attacker, combat);
        Core2026Proof.Check(combat.Damage == 5, "combat hit increased by one");
    });
}
