using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_SeteshProof
{
    internal static void Run() => StandardSetsProof.Case("BE_557", (g, p, id) => {
        // timers/{setesh-conversion,setesh-full-board,timer-owner-counts}.yaml: owner-approved C/D/E assumptions.
        p.Hero.Armor = 3;
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.Hero.Armor == 23, "battlecry adds twenty Armor");
        source.Silence(); Kill(g, source);
        End(g); Core2026Proof.Check(p.Hero.Armor == 23, "opponent start does not tick");
        End(g); Core2026Proof.Check(p.Hero.Armor == 23 && !p.BoardZone.Any(m => m.Card.Id == "BE_561t"), "first owner start waits");
        p.Hero.Armor = 11;
        var clone = g.CloneExact(history: true); End(clone); End(clone);
        var golem = clone.Player1.BoardZone.Single(m => m.Card.Id == "BE_561t");
        Core2026Proof.Check(golem.AttackDamage == 11 && golem.Health == 11 && golem.HasTaunt && golem.Card.Cost == 3 && clone.Player1.Hero.Armor == 0,
            "second owner start converts current Armor to exact Steel Golem stats after source dies");
        Core2026Proof.Check(p.Hero.Armor == 11 && p.BoardZone.Count == 0, "timer clone isolated");
        LocationProofs.Location(p); while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        End(g); End(g); Core2026Proof.Check(p.Hero.Armor == 0 && p.BoardZone.SlotCount == 7, "full physical board consumes Armor once");
        ParkBoard(p); End(g); End(g); Core2026Proof.Check(p.BoardZone.SlotCount == 0, "failed summon has no retry");
        source = (Minion)Play(g, p, id); p.Hero.Armor = 0; Kill(g, source);
        End(g); End(g); End(g); End(g);
        Core2026Proof.Check(p.BoardZone.SlotCount == 0 && !p.AppliedEnchantments.Any(e => e.Card.Id == "BE_557e2"), "zero Armor consumes timer without summoning");
        source = (Minion)Play(g, p, id); Kill(g, source); source = (Minion)Play(g, p, id); Kill(g, source);
        End(g); End(g); End(g); End(g);
        Core2026Proof.Check(p.BoardZone.Count == 1 && p.BoardZone[0].Health == 40 && p.Hero.Armor == 0, "independent timers convert sequentially, not duplicate Armor");
    });
}
