using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class MythicalRunebearProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_481", (g, p, id) => {
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 1 && p.BoardZone.Single().HasTaunt, "3 Attack does not copy");
        ClearBoard(g, p);
        var hand = (Minion)Add(p, id);
        var buffer = Play(g, p, "CORE_CFM_753");
        var enchantments = hand.AppliedEnchantments.Select(e => e.Card.Id).ToArray();
        Core2026Proof.Check(hand.AttackDamage == 4 && hand.Health == 5 && enchantments.Length > 0, "real hand enchantment reaches 4 Attack boundary");
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        p.BaseMana = 10; p.UsedMana = 0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, hand, zonePosition: left.ZonePosition + 1)), "middle play accepted");
        var bears = p.BoardZone.Where(m => m.Card.Id == id).ToArray();
        Core2026Proof.Check(bears.Length == 2 && bears.All(m => m.AttackDamage == 4 && m.Health == 5 && m.HasTaunt
            && m.AppliedEnchantments.Select(e => e.Card.Id).SequenceEqual(enchantments)), "copy keeps hand buffs, enchantments and keywords");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { buffer.Id, left.Id, hand.Id, bears.Single(m => m.Id != hand.Id).Id, right.Id }), "copy immediately right of source between neighbours");
        var copy = bears.Single(m => m.Id != hand.Id); Play(g, p, "BE_228", copy);
        Core2026Proof.Check(copy.AttackDamage == 3 && copy.Health == 4 && hand.AttackDamage == 4, "copy enchantments silence independently");
        ClearBoard(g, p); for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        hand = (Minion)Add(p, id); Play(g, p, "CORE_CFM_753"); PlayEntity(g, p, hand);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == id) == 1, "full board suppresses buffed copy");
    });
}
