using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SoldieroftheBronzeProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_720", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 5 && source.Health == 6 && source.HasTaunt && source.AppliedEnchantments.Any(e => e.Card.Id == "TIME_720e"), "doubles Health with client enchantment");
        var hand = (Minion)Add(p, id); hand.BaseHealth = 5; hand.Damage = 2; PlayEntity(g, p, hand);
        Core2026Proof.Check(hand.Health == 6, "doubles current damaged Health, not maximum Health");
        source.Silence(); Core2026Proof.Check(source.Health == 3, "silence removes doubled Health");
    });
}
