using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class Unpinned_AssimilatingOozeProof
{
    internal static void Run() => StandardSetsProof.Case("BE_734", (g, p, id) => {
        // timers/ooze-delayed-copy.yaml: owner-approved C/D assumption; source-bound end-turn copy, no inherited pending trigger.
        LocationProofs.Location(p); var source = PlayAt(g, p, id, 1); var right = Summon(p, "CS2_182");
        source.AttackDamage += 4; source.BaseHealth += 5; source.Damage = 2;
        var clone = g.CloneExact(history: true); End(clone);
        var copy = clone.Player1.BoardZone.Single(m => m.Card.Id == id && m.Id != source.Id);
        Core2026Proof.Check(copy.AttackDamage == 7 && copy.Health == 5 && copy.Damage == 2 && copy.ZonePosition == 2,
            "current buffs/damage copied right of living source on mixed board");
        Core2026Proof.Check(p.BoardZone.SlotCount == 3, "delayed trigger clone isolated");
        End(g); copy = p.BoardZone.Single(m => m.Card.Id == id && m.Id != source.Id);
        Core2026Proof.Check(p.BoardZone.GetSlot(2) == copy && p.BoardZone.GetSlot(3) == right, "copy preserves right neighbor");
        End(g); End(g); Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == id) == 2, "spent trigger absent on source and copy"); End(g);
        ParkBoard(p); source = (Minion)Play(g, p, id); source.Silence(); End(g);
        Core2026Proof.Check(p.BoardZone.Count == 1, "Silence cancels delayed copy"); End(g);
        ParkBoard(p); source = (Minion)Play(g, p, id); Kill(g, source); End(g); Core2026Proof.Check(p.BoardZone.Count == 0, "death cancels copy"); End(g);
        source = (Minion)Play(g, p, id); new ReturnHandTask(EntityType.SOURCE).Process(g, p, source, null);
        End(g); Core2026Proof.Check(p.BoardZone.Count == 0, "bounce cancels copy"); End(g);
        source = (Minion)Play(g, p, id); LocationProofs.Location(p); while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        End(g); Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == id) == 1, "full physical board consumes one-shot without copy");
        End(g); Kill(g, p.BoardZone.Last()); End(g); Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == id) == 1, "failed copy does not retry");
    });
}
