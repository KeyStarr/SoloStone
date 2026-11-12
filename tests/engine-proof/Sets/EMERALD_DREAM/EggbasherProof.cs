using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class EggbasherProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_468", (g, p, id) => {
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Single().AttackDamage == 3, "optional target on empty board");
        var enemy = Summon(p.Opponent, "CS2_182"); Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Damage == 1 && enemy.AttackDamage == 8 && enemy.AppliedEnchantments.Any(e => e.Card.Id == "EDR_468e1"), "damage then client +4 Attack enchantment");
        var shield = Summon(p, "EX1_008"); shield.HasDivineShield = true; Play(g, p, id, shield);
        Core2026Proof.Check(shield.Damage == 0 && shield.AttackDamage == 5, "shield stops damage, not buff");
        var lethal = Summon(p.Opponent, "EX1_506a"); Play(g, p, id, lethal);
        Core2026Proof.Check(lethal.Zone.Type == Zone.GRAVEYARD, "one Health target dies");
        TargetRules(g, p, id, enemy, shield, false);
    });
}
