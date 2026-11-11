using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class KingMuklaProof
{
    internal static void Run() => Case("CORE_EX1_014", (g, p, id) => {
        Play(g, p, id);
        Core2026Proof.Check(p.Opponent.HandZone.Count == 2 && p.Opponent.HandZone.All(c => c.Card.Id == "EX1_014t"), "Mukla gives two Bananas");
    });
}
