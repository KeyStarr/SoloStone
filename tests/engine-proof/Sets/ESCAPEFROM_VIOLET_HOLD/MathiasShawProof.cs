using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class MathiasShawProof
{
    internal static void Run() => StandardSetsProof.Case("CAP_005", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.HasStealth, "printed Stealth");
        var ordinary = Summon(p, "EX1_008"); var hand = Add(p, "CS2_200");
        Attack(g, ordinary, p.Opponent.Hero); Core2026Proof.Check(hand.Cost == 6, "non-Stealthed attack gives no discount");
        var hidden = Summon(p, "EX1_008"); hidden.HasStealth = true;
        var clone = g.Clone(); Attack(clone, (Minion)clone.IdEntityDic[hidden.Id], clone.Player2.Hero);
        Core2026Proof.Check(clone.IdEntityDic[hand.Id][GameTag.COST] == 3 && hand.Cost == 6, "clone discount independent and visible as COST");
        Attack(g, hidden, p.Opponent.Hero);
        Core2026Proof.Check(hand.Cost == 3 && hand[GameTag.COST] == 3 && hand.AppliedEnchantments.Any(e => e.Card.Id == "CAP_005e4"), "exact pinned cost enchantment");
        Attack(g, source, p.Opponent.Hero); Core2026Proof.Check(hand.Cost == 0, "self attack stacks discounts to zero");
        var second = Add(p, "CS2_029"); End(g);
        var enemy = Summon(p.Opponent, "EX1_008"); enemy.HasStealth = true; Attack(g, enemy, p.Hero);
        Core2026Proof.Check(second.Cost == 4, "opponent Stealth does not discount"); End(g);
        Core2026Proof.Check(hand.Cost == 0, "discount persists across turns");
        source.Silence(); hidden.HasStealth = true; Attack(g, hidden, p.Opponent.Hero);
        Core2026Proof.Check(second.Cost == 4, "silenced source does not discount");
        Kill(g, source);
        var fresh = (Minion)Play(g, p, id);
        Core2026Proof.Check(!fresh.IsSilenced && fresh.ActivatedTrigger != null, "fresh Shaw trigger active before removal");
        Kill(g, fresh); End(g); End(g);
        // Every possible random target is observable, so a leaked discount cannot hide on a zero-cost card.
        foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
        var afterDeath = Add(p, "CS2_200"); hidden.HasStealth = true; Attack(g, hidden, p.Opponent.Hero);
        Core2026Proof.Check(afterDeath.Cost == 6, "dead unsilenced source does not discount");
        ClearBoard(g, p); foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
        source = (Minion)Play(g, p, id); Attack(g, source, p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.Count == 0, "empty hand is harmless");
        End(g); End(g); source.HasStealth = true;
        var one = Add(p, "CS2_200"); var two = Add(p, "CS2_200");
        // Remove the turn draw so both random candidates have the same starting cost.
        foreach (var card in p.HandZone.Where(c => c.Id != one.Id && c.Id != two.Id).ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
        Attack(g, source, p.Opponent.Hero);
        Core2026Proof.Check(new[] { one.Cost, two.Cost }.OrderBy(n => n).SequenceEqual(new[] { 3, 6 }), "exactly one random hand card discounted");
    });
}
