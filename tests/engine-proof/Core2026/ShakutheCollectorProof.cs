using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class ShakutheCollectorProof
{
    internal static void Run() => Case("CORE_CFM_781", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Attack(g, source, p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.Count == 1 && p.HandZone[0].Card.Class != p.HeroClass && p.HandZone[0].Card.Class != CardClass.NEUTRAL, "attack gets another-class card");
    });
}
