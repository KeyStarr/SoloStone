using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static EmeraldDreamBatch3Proof;

internal static class GrotesqueRunebladeProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_812", (g, p, id) => {
        var weapon = (Weapon)Play(g, p, id);
        Core2026Proof.Check(weapon.AttackDamage == 2 && weapon.Durability == 2, "first play has no preceding rune card");
        var clone = g.Clone(); var copiedWeapon = (Weapon)Play(clone, clone.Player1, id);
        Core2026Proof.Check(copiedWeapon.AttackDamage == 3 && copiedWeapon.Durability == 3
            && weapon.AttackDamage == 2 && weapon.Durability == 2 && p.PlayHistory.Count == 1, "clone history and weapon enchantments isolated");
        weapon = (Weapon)Play(g, p, id);
        Core2026Proof.Check(weapon.AttackDamage == 3 && weapon.Durability == 3
            && weapon.AppliedEnchantments.Any(e => e.Card.Id == "EDR_812e") && weapon.AppliedEnchantments.Any(e => e.Card.Id == "EDR_812e1"), "preceding Blood/Unholy weapon grants both exact enchantments");
        Play(g, p, "CS2_171"); weapon = (Weapon)Play(g, p, id);
        Core2026Proof.Check(weapon.AttackDamage == 2 && weapon.Durability == 2, "only immediately previous card counts");
        Play(g, p, "EDR_816"); weapon = (Weapon)Play(g, p, id);
        Core2026Proof.Check(weapon.AttackDamage == 2 && weapon.Durability == 2, "Death Knight class alone does not imply rune cost");
        var unholy = Cards.FromId(id).Clone(); unholy.Tags.Remove((GameTag)2196);
        p.PlayHistory.Add(new PlayHistoryEntry(unholy)); weapon = (Weapon)Play(g, p, id);
        Core2026Proof.Check(weapon.AttackDamage == 3 && weapon.Durability == 2, "Unholy-only previous card grants Attack only");
        var blood = Cards.FromId(id).Clone(); blood.Tags.Remove((GameTag)2198);
        p.PlayHistory.Add(new PlayHistoryEntry(blood)); weapon = (Weapon)Play(g, p, id);
        Core2026Proof.Check(weapon.AttackDamage == 2 && weapon.Durability == 3
            && weapon.AppliedEnchantments.Any(e => e.Card.Id == "EDR_812e1")
            && !weapon.AppliedEnchantments.Any(e => e.Card.Id == "EDR_812e"), "Blood-only previous card grants Durability only");
        End(g); Play(g, p.Opponent, id); End(g); weapon = (Weapon)Play(g, p, id);
        Core2026Proof.Check(weapon.AttackDamage == 3 && weapon.Durability == 3, "own last card persists over turns; opponent plays ignored");
    });
}
