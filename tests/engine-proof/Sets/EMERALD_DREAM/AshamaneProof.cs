using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static EmeraldDreamBatch3Proof;

internal static class AshamaneProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_527", (g, p, id) => {
        EmptyDeck(p.Opponent); var original = InDeck(p.Opponent, "CS2_029"); original.Cost = 3;
        var held = Add(p, "CS2_182"); var source = (Minion)Play(g, p, id);
        var copies = p.HandZone.Where(c => c.Id != held.Id).ToArray();
        Core2026Proof.Check(p.HandZone.Count == 10 && copies.Length == 9 && copies.All(c => c.Card.Id == "CS2_029" && c.Id != original.Id), "fills hand with fresh opponent-deck copies");
        foreach (var card in copies) Cost(card, 0); Cost(held, 4); Cost(original, 3);
        CopyCost(copies[0], 0); PublishedCost(copies[0], 0);
        Core2026Proof.Check(p.Opponent.DeckZone.Single().Id == original.Id, "opponent deck preserved");
        int count = g.IdEntityDic.Count; Battlecry(g, p, source); Core2026Proof.Check(g.IdEntityDic.Count == count, "full hand creates nothing");
        source.Silence(); Kill(g, source); End(g); End(g); foreach (var card in copies) Cost(card, 0);
        var clone = g.Clone(); PlayEntity(clone, clone.Player1, clone.IdEntityDic[copies[0].Id], clone.Player2.Hero);
        Core2026Proof.Check(p.HandZone.Contains(copies[0]) && p.Opponent.Hero.Damage == 0, "clone copied spell play independent");
        foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
        EmptyDeck(p.Opponent); source = (Minion)Play(g, p, id); Core2026Proof.Check(p.HandZone.Count == 0, "empty opponent deck generates nothing");
        InDeck(p.Opponent, "CS2_182"); InDeck(p.Opponent, "EDR_470");
        Battlecry(g, p, source); Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.All(c => c.Card.Id is "CS2_182" or "EDR_470"), "sampling restricted to current opponent deck");
        foreach (var minion in p.HandZone.OfType<Minion>()) {
            Core2026Proof.Check(minion.AttackDamage == minion.Card.ATK && minion.Health == minion.Card.Health, "copied minion stats");
            Cost(minion, Math.Max(0, minion.Card.Cost - 3));
        }
    });
}
