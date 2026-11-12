using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class FatebreakerProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_028", (g, p, id) => {
        var none = (Minion)Play(g, p, id); Core2026Proof.Check(none.AttackDamage == 4 && none.Health == 4 && none.HasLifeSteal, "no Shred means no buff");
        Add(p, "TIME_025t"); var heldOnly = (Minion)Play(g, p, id); Core2026Proof.Check(heldOnly.AttackDamage == 4 && p.Hero.Damage == 0, "held Shred does not count");
        var shred = Entity.FromCard(p, Cards.FromId("TIME_025t")); p.DeckZone.Add(shred); int count = p.DeckZone.Count; int hand = p.HandZone.Count;
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 7 && source.Health == 7 && source.HasLifeSteal && source.AppliedEnchantments.Any(e => e.Card.Id == "TIME_028e"), "deck Shred grants pinned +3/+3");
        Core2026Proof.Check(shred.Zone.Type == Zone.GRAVEYARD && p.DeckZone.Count == count - 1 && p.HandZone.Count == hand && p.Hero.Damage == 3, "cast consumes deck Shred, deals damage, no replacement draw");
        source.Silence(); Core2026Proof.Check(source.AttackDamage == 4 && source.Health == 4 && !source.HasLifeSteal, "Silence removes buff and Lifesteal");
        ClearBoard(g, p); var first = Entity.FromCard(p, Cards.FromId("TIME_025t")); var second = Entity.FromCard(p, Cards.FromId("TIME_025t")); p.DeckZone.Add(first); p.DeckZone.Add(second);
        Play(g, p, id); Core2026Proof.Check(p.DeckZone.Count(c => c.Card.Id == "TIME_025t") == 1 && p.Hero.Damage == 6, "multiple Shreds consume exactly one");
    });
}
