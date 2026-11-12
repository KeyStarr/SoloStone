using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class StoryOfBarnabusProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_231", (g, p, id) => {
        foreach (var item in p.DeckZone.ToArray()) { p.DeckZone.Remove(item); p.SetasideZone.Add(item); }
        var high = (Minion)Entity.FromCard(p, Cards.FromId("CS2_186"), zone: p.DeckZone);
        Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone);
        Play(g, p, id); Core2026Proof.Check(high.Zone == p.HandZone && high.AttackDamage == 7 && high.BaseHealth == 12 && p.Hero.Armor == 5, "draw minion only, at least five Attack gives five Health and Armor");
        var low = (Minion)Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone);
        Play(g, p, id); Core2026Proof.Check(low.Zone == p.HandZone && low.BaseHealth == 5 && p.Hero.Armor == 5, "four Attack gives no bonus");
        var boundary = (Minion)Entity.FromCard(p, Cards.FromId("CS2_182"), zone: p.DeckZone); boundary.AttackDamage = 5;
        Play(g, p, id); Core2026Proof.Check(boundary.BaseHealth == 10 && p.Hero.Armor == 10, "current five Attack qualifies");
        int deck = p.DeckZone.Count; Play(g, p, id);
        Core2026Proof.Check(p.DeckZone.Count == deck && p.Hero.Armor == 10, "no matching minion does not draw spell");
        var burned = (Minion)Entity.FromCard(p, Cards.FromId("CS2_186"), zone: p.DeckZone);
        var source = Entity.FromCard(p, Cards.FromId(id)); while (p.HandZone.Count < 10) Add(p, "CS2_182");
        source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
        Core2026Proof.Check(burned.Zone == p.GraveyardZone && burned.BaseHealth == 7 && p.Hero.Armor == 10, "burned draw gains neither Health nor Armor");
    });
}
