using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class ArchmageAntonidasProof
{
    internal static void Run() => Case("CORE_EX1_559", (g, p, id) => {
        Play(g, p, id); Play(g, p, "GAME_005");
        Core2026Proof.Check(p.HandZone.Single().Card.Id == "CS2_029", "Antonidas adds Fireball after spell");
    });
}
