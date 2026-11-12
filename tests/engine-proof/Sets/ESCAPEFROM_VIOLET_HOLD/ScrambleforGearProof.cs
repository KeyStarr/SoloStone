using SabberStoneCore.Model.Zones;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ScrambleforGearProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_386", (g, p, id) => {

        var old = p.DeckZone.Select(c => c.Id).ToHashSet(); Play(g, p, id);
        var gears = p.DeckZone.Where(c => c.Card.Id == "JAIL_386t").ToArray();
        Core2026Proof.Check(p.Hero.Armor == 2 && gears.Length == 5 && gears.All(c => c.Cost == 1 && c.Card.Implemented) && p.DeckZone.Count == old.Count + 5 && old.All(i => p.DeckZone.Any(c => c.Id == i)), "gain two Armor; shuffle five exact Gear spells without losing existing cards");
        var clone = g.Clone(); Generic.Draw(clone.Player1, clone.IdEntityDic[gears[0].Id]); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(clone.Player1.Hero.Armor == 4 && p.Hero.Armor == 2 && gears[0].Zone == p.DeckZone, "draw effect isolated in clone");
        Generic.Draw(p, gears[0]); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.Hero.Armor == 4 && p.HandZone.Count == 1 && p.HandZone.Single().Card.Id == "CS2_182" && gears[0].Zone != p.HandZone, "drawn Gear gives Armor and replacement draw");
        while (p.HandZone.Count < 10) Add(p, "CS2_182"); int count = p.DeckZone.Count;
        Generic.Draw(p, gears[1]); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.Hero.Armor == 4 && p.DeckZone.Count == count - 1 && p.HandZone.Count == 10, "burned Gear gives neither armor nor replacement");
        foreach (var c in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(c));
        foreach (var c in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(c));
        Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        for (int i = 0; i < 2; i++) Entity.FromCard(p, Cards.FromId("JAIL_386t"), zone: p.DeckZone);
        Generic.Draw(p); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.Hero.Armor == 8 && p.HandZone.Single().Card.Id == "CS2_182" && p.DeckZone.Count == 0, "consecutive Gears each trigger and replace");
        while (p.DeckZone.Count < (DeckZone.DeckMaximumCapcity - 1)) Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        Play(g, p, id); Core2026Proof.Check(p.DeckZone.Count == DeckZone.DeckMaximumCapcity && p.DeckZone.Count(c => c.Card.Id == "JAIL_386t") == 1, "shuffle respects deck capacity");
    });
}
