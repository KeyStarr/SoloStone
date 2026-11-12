using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class DoomguardProof
{
    internal static void Run() => Case("CORE_EX1_310", (g, p, id) => {
        Add(p, "CS2_182"); Add(p, "CS2_029"); var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 0 && p.GraveyardZone.Count == 2 && source.HasCharge, "Doomguard discards two and has Charge");
    });
}
