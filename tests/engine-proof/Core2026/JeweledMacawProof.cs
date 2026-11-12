using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class JeweledMacawProof
{
    internal static void Run() => Case("CORE_UNG_912", (g, p, id) => {
        Play(g, p, id); Core2026Proof.Check(p.HandZone.Single().Card.IsRace(Race.BEAST), "Macaw generates Beast");
    });
}
