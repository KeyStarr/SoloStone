using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class SleepyDragonProof
{
    internal static void Run() => Case("CORE_LOOT_137", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.AttackDamage == 6 && source.Health == 12 && source.HasTaunt, "Sleepy Dragon base 6/12 Taunt");
    });
}
