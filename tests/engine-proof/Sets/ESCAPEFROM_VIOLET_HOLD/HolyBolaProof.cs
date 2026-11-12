using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class HolyBolaProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_377", (g, p, id) => {
        int deck = p.DeckZone.Count; Play(g, p, id);
        Core2026Proof.Check(p.DeckZone.Count == deck - 1 && p.HandZone.Count == 1, "expensive first draw stops");
        foreach (var c in p.DeckZone.ToArray()) c.Cost = 2;
        deck = p.DeckZone.Count; Play(g, p, id);
        Core2026Proof.Check(p.DeckZone.Count == deck - 2 && p.HandZone.Count == 3, "cost two boundary draws twice");
        foreach (var c in p.DeckZone.ToArray()) c.Cost = 0;
        deck = p.DeckZone.Count; Play(g, p, id); Core2026Proof.Check(p.DeckZone.Count == deck - 2, "zero cost draws twice");
        while (p.HandZone.Count < 10) Add(p, "CS2_182");
        var spell = p.HandZone.First(); p.HandZone.Remove(spell); p.SetasideZone.Add(spell);
        deck = p.DeckZone.Count; Play(g, p, id); Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 2, "second draw burns when hand fills");
        foreach (var c in p.DeckZone.ToArray()) { p.DeckZone.Remove(c); p.SetasideZone.Add(c); }
        p.HandZone.Remove(p.HandZone.First());
        int health = p.Hero.Health; Play(g, p, id); Core2026Proof.Check(p.Hero.Health == health - 1, "empty deck fatigues only once");
    });
}
