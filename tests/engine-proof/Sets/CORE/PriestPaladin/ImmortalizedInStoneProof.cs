using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ImmortalizedInStoneProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_TSC_076", (g, p, id) => {
        var left = Summon(p, "CS2_182"); Play(g, p, id);
        var statues = p.BoardZone.Skip(1).ToArray();
        Core2026Proof.Check(p.BoardZone[0] == left && statues.Select(m => m.Card.Id).SequenceEqual(new[] { "TSC_076t3", "TSC_076t2", "TSC_076t" }), "tokens append in 4/8, 2/4, 1/2 order");
        Core2026Proof.Check(statues.Select(m => m.AttackDamage).SequenceEqual(new[] { 4, 2, 1 }) && statues.Select(m => m.Health).SequenceEqual(new[] { 8, 4, 2 })
            && statues.All(m => m.HasTaunt && m.IsRace(Race.ELEMENTAL) && m.Card.Implemented), "exact token stats, Elemental, Taunt");
        foreach (int occupied in new[] { 5, 6, 7 })
        {
            ClearBoard(g, p); for (int i = 0; i < occupied; i++) Summon(p, "CS2_182"); Play(g, p, id);
            Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Skip(occupied).Select(m => m.Card.Id).SequenceEqual(new[] { "TSC_076t3", "TSC_076t2" }.Take(7 - occupied)), "limited board retains specified summon order");
        }
    });
}
