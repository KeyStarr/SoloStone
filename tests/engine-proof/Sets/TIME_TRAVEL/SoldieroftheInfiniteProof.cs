using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SoldieroftheInfiniteProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_051", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 6 && source.Health == 5 && source.IsRush && source.AppliedEnchantments.Any(e => e.Card.Id == "TIME_051e"), "doubles attack with client enchantment");
        var enemy = Summon(p.Opponent, "CS2_182");
        AttackOptionsProof.Lifecycle(g, source, enemy, rush: true);
        var hand = (Minion)Add(p, id); hand.AttackDamage = 5; PlayEntity(g, p, hand);
        Core2026Proof.Check(hand.AttackDamage == 10, "doubles current buffed Attack");
        AttackOptionsProof.Lifecycle(g, hand, enemy, rush: true);
        source.Silence();
        Core2026Proof.Check(source.AttackDamage == 3, "silence removes doubled Attack");
    });
}
