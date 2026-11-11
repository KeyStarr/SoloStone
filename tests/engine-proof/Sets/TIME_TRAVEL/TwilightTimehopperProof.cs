using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TwilightTimehopperProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_025", (g, p, id) => {
        int count = p.DeckZone.Count; Play(g, p, id);
        var shreds = p.DeckZone.Where(c => c.Card.Id == "TIME_025t").ToArray();
        Core2026Proof.Check(p.DeckZone.Count == count + 2 && shreds.Length == 2 && shreds.All(c => c.Card.Cost == 0 && c.Card.Implemented), "two exact Shreds shuffled");
        int hand = p.HandZone.Count; Generic.Draw(p, shreds[0]); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.Hero.Damage == 3 && p.HandZone.Count == hand + 1 && shreds[0].Zone.Type == Zone.GRAVEYARD, "drawn Shred casts and draws replacement");
        Summon(p, "TIME_856"); Generic.Draw(p, shreds[1]); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.Hero.Damage == 8 && shreds[1].Zone.Type == Zone.GRAVEYARD, "Shred spell damage includes spell power");
        ClearBoard(g, p); p.Hero.Damage = 0;
        var burned = Entity.FromCard(p, Cards.FromId("TIME_025t")); p.DeckZone.Add(burned);
        while (p.HandZone.Count < 10) Add(p, "CS2_182");
        Generic.Draw(p, burned); Core2026Proof.Check(p.Hero.Damage == 0 && burned.Zone.Type == Zone.GRAVEYARD && p.HandZone.Count == 10, "burned Shred neither casts nor draws");
        foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        Play(g, p, id); Generic.Draw(p); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.Hero.Damage == 7 && p.DeckZone.Count == 0 && p.Hero.Fatigue == 1, "chained Shreds each deal three then replacement fatigues");
    });
}
