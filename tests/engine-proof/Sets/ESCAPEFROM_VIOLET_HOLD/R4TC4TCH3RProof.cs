using SabberStoneCore.Model.Zones;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class R4TC4TCH3RProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_882", (g, p, id) => {
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        var spells = new[] { Entity.FromCard(p, Cards.FromId("CS2_029")), Entity.FromCard(p, Cards.FromId("CS2_023")) };
        foreach (var spell in spells) p.DeckZone.Add(spell); spells[0].Cost = 2;
        var minion = Entity.FromCard(p, Cards.FromId("CS2_182")); p.DeckZone.Add(minion);
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.Card.GetRawRace() == Race.MECHANICAL && p.DeckZone.Count == 5 && p.HandZone.Count == 0, "Battlecry copies spells into deck, not hand");
        Core2026Proof.Check(spells.All(s => p.DeckZone.Count(c => c.Card.Id == s.Card.Id) == 2) && p.DeckZone.Count(c => c is Minion) == 1, "every spell copied once; minions not copied");
        Core2026Proof.Check(p.DeckZone.Where(c => c.Card.Id == "CS2_029").All(c => c.Cost == 2), "deck copies retain cost modifications");
        Kill(g, source); Core2026Proof.Check(p.HandZone.Count == 1 && p.HandZone.Single() is Spell && minion.Zone == p.DeckZone && p.DeckZone.Count == 4, "Deathrattle draws a spell only");
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_182"))); source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(p.DeckZone.Count == 1 && p.HandZone.Count == 1 && p.Hero.Damage == 0, "no spells means no copies, draw or fatigue");
        p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_029"))); source = Summon(p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.DeckZone.Count == 2 && p.HandZone.Count == 1, "silence removes draw");
        source = Summon(p, id); while (p.HandZone.Count < 10) Add(p, "CS2_182"); Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == 1, "full hand burns drawn spell");
        p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_029"))); while (p.DeckZone.Count < (DeckZone.DeckMaximumCapcity - 1)) p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_182")));
        foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
        Play(g, p, id); Core2026Proof.Check(p.DeckZone.Count == DeckZone.DeckMaximumCapcity, "deck copy respects capacity");
    });
}
