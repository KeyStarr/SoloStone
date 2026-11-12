using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TroubledDoubleProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_710", (g, p, id) => {
        var first = (Minion)Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 1 && first.HasStealth, "first card has no Combo");
        ClearBoard(g, p); End(g); End(g);
        var hand = (Minion)Add(p, id); var buffer = Play(g, p, "CORE_CFM_753");
        var enchantments = hand.AppliedEnchantments.Select(e => e.Card.Id).ToArray();
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        p.BaseMana = 10; p.UsedMana = 0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, hand, zonePosition: left.ZonePosition + 1)), "middle play accepted");
        var doubles = p.BoardZone.Where(m => m.Card.Id == id).ToArray();
        Core2026Proof.Check(doubles.Length == 2 && doubles.All(m => m.AttackDamage == 4 && m.Health == 4 && m.HasStealth
            && m.AppliedEnchantments.Select(e => e.Card.Id).SequenceEqual(enchantments)) && enchantments.Length > 0, "second-card Combo copies real hand buffs and Stealth");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { buffer.Id, left.Id, hand.Id, doubles.Single(m => m.Id != hand.Id).Id, right.Id }), "copy immediately right of source between neighbours");
        var copy = doubles.Single(m => m.Id != hand.Id); Play(g, p, "BE_228", copy);
        Core2026Proof.Check(copy.AttackDamage == 3 && copy.Health == 3 && !copy.HasStealth && hand.AttackDamage == 4, "copy enchantments silence independently");
        ClearBoard(g, p); for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == id) == 1, "full board suppresses Combo copy");
    });
}
