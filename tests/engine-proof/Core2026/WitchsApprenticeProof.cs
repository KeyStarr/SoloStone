using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class WitchsApprenticeProof
{
    internal static void Run() => Case("CORE_GIL_531", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.HasTaunt && p.HandZone.Single().Card.Class == CardClass.SHAMAN && p.HandZone.Single().Card.Type == CardType.SPELL, "Apprentice generates Shaman spell");
    });
}
