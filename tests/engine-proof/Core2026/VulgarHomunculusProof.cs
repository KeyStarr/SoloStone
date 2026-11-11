using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class VulgarHomunculusProof
{
    internal static void Run() => Case("CORE_LOOT_013", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.HasTaunt && p.Hero.Damage == 2, "Homunculus damages own hero 2");
    });
}
