using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class SwashburglarProof
{
    internal static void Run() => Case("CORE_KAR_069", (g, p, id) => {
        Play(g, p, id); Core2026Proof.Check(p.HandZone.Count == 1 && p.HandZone[0].Card.Class != p.HeroClass && p.HandZone[0].Card.Class != CardClass.NEUTRAL, "Swashburglar generates another-class card");
    });
}
