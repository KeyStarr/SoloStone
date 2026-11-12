using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class SneedsOldShredderProof
{
    internal static void Run() => Case("CORE_GVG_114", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Single().Card.Rarity == Rarity.LEGENDARY, "Shredder summons Legendary");
    });
}
