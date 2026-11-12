using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static EmeraldDreamBatch3Proof;

internal static class ZaqaliFlamemancerProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_940", (g, p, id) => {
        var one = Add(p, "CS1_042"); var four = Add(p, "CS2_029"); var six = Add(p, "CS2_162");
        var enemy = Add(p.Opponent, "CS2_029"); var source = (Minion)Play(g, p, id);
        Cost(one, 0); Cost(four, 2); Cost(six, 4); Cost(enemy, 4);
        CopyCost(one, 0); CopyCost(four, 2); CopyCost(six, 4); PublishedCost(four, 2);
        var clone = g.Clone(); Battlecry(clone, clone.Player1, (Minion)clone.IdEntityDic[source.Id]);
        Cost(clone.IdEntityDic[four.Id], 0); Cost(four, 2);
        var same = Add(p, "CS2_029");
        Battlecry(g, p, source); // Costs are 0,2,4,4: one duplicate prevents every reduction.
        Cost(one, 0); Cost(four, 2); Cost(six, 4); Cost(same, 4);
        p.HandZone.Remove(same); p.SetasideZone.Add(same);
        End(g); End(g); Cost(four, 2); source.Silence(); Cost(four, 2); Kill(g, source); Cost(four, 2);
        foreach (var card in p.HandZone.ToArray()) { p.HandZone.Remove(card); p.SetasideZone.Add(card); }
        source = (Minion)Play(g, p, id); Core2026Proof.Check(p.HandZone.Count == 0, "empty hand vacuously qualifies safely");
    });
}
