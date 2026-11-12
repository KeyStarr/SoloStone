using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class RuinousVelocidrakeProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_029", (g, p, id) => {
        var none = (Minion)Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 1 && none.IsRush, "no deck Shred means no copy");
        ClearBoard(g, p); Add(p, "TIME_025t"); Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 1, "held Shred does not count"); ClearBoard(g, p);
        var shred = Entity.FromCard(p, Cards.FromId("TIME_025t")); p.DeckZone.Add(shred);
        var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182"); var source = (Minion)Add(p, id); source.AttackDamage = 7; source.BaseHealth = 8;
        p.UsedMana = 0; Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 1)), "middle play accepted");
        var copy = p.BoardZone.Single(m => m.Card.Id == id && m.Id != source.Id);
        Core2026Proof.Check(copy.AttackDamage == 7 && copy.Health == 8 && copy.IsRush && p.Hero.Damage == 3 && shred.Zone.Type == Zone.GRAVEYARD, "cast Shred then copy current stats and printed Rush");
        Core2026Proof.Check(p.BoardZone.Select(m => m.Id).SequenceEqual(new[] { left.Id, source.Id, copy.Id, right.Id }), "copy right of source");
        ClearBoard(g, p); for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        shred = Entity.FromCard(p, Cards.FromId("TIME_025t")); p.DeckZone.Add(shred); Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Count(m => m.Card.Id == id) == 1 && shred.Zone.Type == Zone.GRAVEYARD && p.Hero.Damage == 6, "full board prevents copy but still casts Shred");
    });
}
