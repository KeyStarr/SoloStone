using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class HolyEggbearerProof
{
    internal static void Run() => StandardSetsProof.Case("DINO_411", (g, p, id) => {
        int deck = p.DeckZone.Count; var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.DeckZone.Count == deck && p.HandZone.Count == 0 && p.Hero.Damage == 0, "no zero-Attack minion means no draw or fatigue");
        var spell = Entity.FromCard(p, Cards.FromId("CS2_029")); p.DeckZone.Add(spell);
        var egg = Entity.FromCard(p, Cards.FromId("EX1_557")); p.DeckZone.Add(egg);
        Play(g, p, id); var drawn = (Minion)p.HandZone.Single();
        Core2026Proof.Check(drawn.Id == egg.Id && drawn.Card.Id == "EX1_557" && drawn.AttackDamage == 0 && drawn.Health == 4 && spell.Zone.Type == Zone.DECK, "draws exactly matching 0-Attack minion");
        var another = Entity.FromCard(p, Cards.FromId("EX1_557")); p.DeckZone.Add(another);
        while (p.HandZone.Count < 10) Add(p, "CS2_182");
        source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(p.HandZone.Count == 10 && another.Zone.Type == Zone.GRAVEYARD, "full hand burns selected minion");
    });
}
