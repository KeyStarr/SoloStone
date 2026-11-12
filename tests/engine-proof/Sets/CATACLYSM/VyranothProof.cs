using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_VyranothProof
{
    internal static void Run() => Unpinned_CostMemory();
    // Owner-approved working assumptions: docs/design/costmemory.md.
    private static void Unpinned_CostMemory() => StandardSetsProof.Case("CATA_213", (g, p, id) => {
        p.DeckCards.Clear();
        p.DeckCards.AddRange(Enumerable.Repeat(Cards.FromId("CS2_182"), 20));
        p.DeckCards.AddRange(Enumerable.Repeat(Cards.FromId("CS2_120"), 10));
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        for (int i = 0; i < 7; i++) p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_182")));
        var deck = p.DeckZone.Cast<Minion>().ToArray();
        Play(g, p, id);
        Core2026Proof.Check(deck.Sum(m => m.AttackDamage - m.Card.ATK) == 50 && deck.Sum(m => m.Health - m.Card.Health) == 50,
            "independent +50 Attack and Health totals with remainders");
        Core2026Proof.Check(deck.All(m => m.AttackDamage - m.Card.ATK is 7 or 8 && m.Health - m.Card.Health is 7 or 8),
            "even base allocation and distinct remainder recipients");
        Core2026Proof.Check(CostRules.StartingMinionCost(p) == 100, "starting total independent of current seven-card deck");
        var old = deck.Select(m => (m.AttackDamage,m.Health)).ToArray();
        p.DeckCards.RemoveAt(0);
        Play(g, p, id);
        Core2026Proof.Check(deck.Select(m => (m.AttackDamage,m.Health)).SequenceEqual(old), "false starting condition gives no stats");
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        p.DeckCards.Add(Cards.FromId("CS2_182")); Play(g, p, id);
        Core2026Proof.Check(p.DeckZone.Count == 0, "empty recipient deck fizzles");
    });
}
