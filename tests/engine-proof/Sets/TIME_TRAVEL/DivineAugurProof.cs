using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class DivineAugurProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_429", (g, p, id) => {
        var lowAttack = (Minion)Add(p, "CS2_182"); var lowHealth = (Minion)Add(p, "TIME_059"); var equal = (Minion)Add(p, "TIME_703"); var spell = Add(p, "CS2_029"); var weapon = (Weapon)Add(p, "CS2_106"); var board = Summon(p, "CS2_182");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(lowAttack.AttackDamage == 5 && lowAttack.Health == 5 && lowHealth.AttackDamage == 2 && lowHealth.Health == 2 && equal.AttackDamage == 5 && equal.Health == 5, "higher Health, higher Attack, equal stats");
        Core2026Proof.Check(lowAttack.AppliedEnchantments.Any(e => e.Card.Id == "TIME_429e1") && lowHealth.AppliedEnchantments.Any(e => e.Card.Id == "TIME_429e2"), "pinned enchantments");
        Core2026Proof.Check(board.AttackDamage == 4 && board.Health == 5 && source.AttackDamage == 4 && source.Health == 5 && weapon.AttackDamage == 3 && spell.Cost == 4, "board, source and nonminions excluded");
        Play(g, p, id); Core2026Proof.Check(lowAttack.AttackDamage == 5 && lowHealth.Health == 2, "equalized hand does not grow again");
        PlayEntity(g, p, lowAttack); lowAttack.Silence(); Core2026Proof.Check(lowAttack.AttackDamage == 4 && lowAttack.Health == 5, "Silence removes hand enchantment");
    });
}
