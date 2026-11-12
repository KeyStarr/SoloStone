using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class BrightwingProof
{
    internal static void Run() => Case("CORE_EX1_189", (g, p, id) => {
        Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 1 && p.HandZone[0].Card.Type == CardType.MINION && p.HandZone[0].Card.Rarity == Rarity.LEGENDARY, "Brightwing generates Legendary minion");
    });
}
