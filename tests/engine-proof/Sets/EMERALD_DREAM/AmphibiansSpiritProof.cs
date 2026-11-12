using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class AmphibiansSpiritProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_261", (g, p, id) => {
        var source = Summon(p, "CS2_182"); Play(g, p, id, source);
        Core2026Proof.Check(source.AttackDamage == 6 && source.Health == 7 && source.HasDeathrattle && source.AppliedEnchantments.Any(e => e.Card.Id == "EDR_261e"), "exact +2/+2 and Deathrattle enchantment");
        var next = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); Kill(g, source);
        Core2026Proof.Check(next.AttackDamage == 6 && next.Health == 7 && next.HasDeathrattle && enemy.AttackDamage == 4, "Deathrattle passes stats and itself only to friendly minion");
        var last = Summon(p, "CS2_182"); Kill(g, next); Core2026Proof.Check(last.AttackDamage == 6 && last.HasDeathrattle, "inherited Deathrattle passes again");
        last.Silence(); Core2026Proof.Check(last.AttackDamage == 4 && last.Health == 5 && !last.HasDeathrattle, "silence removes stats and inherited Deathrattle");
        next = Summon(p, "CS2_182"); Kill(g, last); Core2026Proof.Check(next.AttackDamage == 4, "silenced minion cannot pass spirit");
        Play(g, p, id, enemy); var enemyNext = Summon(p.Opponent, "CS2_182"); Kill(g, enemy);
        Core2026Proof.Check(enemyNext.AttackDamage == 6 && enemyNext.HasDeathrattle && next.AttackDamage == 4, "enemy target passes to its own controller's minion");
        var other = Summon(p, "CS2_182"); Play(g, p, id, other); var third = Summon(p, "CS2_182"); Kill(g, other);
        Core2026Proof.Check(new[] { next, third }.Count(m => m.AttackDamage == 6) == 1, "exactly one random friendly recipient");
        ClearBoard(g, p); source = Summon(p, "CS2_182"); Play(g, p, id, source); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Count == 0, "no friendly recipient is safe");
        source = Summon(p, "CS2_182"); Play(g, p, id, source); Play(g, p, id, source);
        Core2026Proof.Check(source.AttackDamage == 8 && source.Health == 9, "multiple spirits stack"); next = Summon(p, "CS2_182"); Kill(g, source);
        Core2026Proof.Check(next.AttackDamage == 8 && next.AppliedEnchantments.Count(e => e.Card.Id == "EDR_261e") == 2, "each attached Deathrattle passes independently");
        TargetRules(g, p, id, enemyNext, next, false);
    });
}
