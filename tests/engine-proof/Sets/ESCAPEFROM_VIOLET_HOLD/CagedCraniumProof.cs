using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class CagedCraniumProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_513", (g, p, id) => {
        var empty = (Minion)Play(g, p, id);
        Core2026Proof.Check(empty.Health == 1 && empty.HasTaunt, "empty hand gives no Health");
        Add(p, "CS2_029"); Add(p, "CS2_182");
        var two = (Minion)Play(g, p, id);
        Core2026Proof.Check(two.Health == 3 && two.AppliedEnchantments.Any(e => e.Card.Id == "JAIL_513e"), $"counts both spell and minion; excludes played source: {two.Health}, ench={string.Join(",", two.AppliedEnchantments.Select(e => e.Card.Id))}");
        while (p.HandZone.Count < 9) Add(p, "CS2_182");
        var full = (Minion)Play(g, p, id);
        Core2026Proof.Check(full.Health == 10 && p.HandZone.Count == 9, "ten-card hand leaves nine cards for battlecry");
        Play(g, p, "BE_228", full); Core2026Proof.Check(full.Health == 1 && !full.HasTaunt, "silence removes gained Health and Taunt");
    });
}
