using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class DimensionalWeaponsmithProof
{
    internal static void Run() => StandardSetsProof.Case("END_021", (g, p, id) => {
        var minion = (Minion)Add(p, "CS2_182"); var weapon = (Weapon)Add(p, "CS2_106"); var spell = Add(p, "CS2_029");
        var board = Summon(p, "CS2_182"); Play(g, p, "CS2_106"); var equipped = p.Hero.Weapon;
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(minion.AttackDamage == 6 && minion.Health == 5 && weapon.AttackDamage == 5 && weapon.Durability == 2, "hand minion and weapon gain exactly two Attack");
        Core2026Proof.Check(board.AttackDamage == 4 && source.AttackDamage == 2 && equipped.AttackDamage == 3 && spell.Cost == 4, "excludes board, source, equipped weapon and spells");
        Core2026Proof.Check(minion.AppliedEnchantments.Any(e => e.Card.Id == "END_021e") && weapon.AppliedEnchantments.Any(e => e.Card.Id == "END_021e"), "pinned buffs on both entity types");
        Play(g, p, id); Core2026Proof.Check(minion.AttackDamage == 8 && weapon.AttackDamage == 7, "repeated buffs stack");
        PlayEntity(g, p, minion); minion.Silence(); Core2026Proof.Check(minion.AttackDamage == 4, "hand buff removable by Silence");
        PlayEntity(g, p, weapon); Core2026Proof.Check(p.Hero.Weapon.AttackDamage == 7, "buff persists when weapon equipped");
    });
}
