using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class GrimestreetOutfitterProof
{
    internal static void Run() => Case("CORE_CFM_753", (g, p, id) => {
        var minion = (Minion)Add(p, "CS2_182"); var spell = Add(p, "CS2_029");
        Play(g, p, id);
        Core2026Proof.Check(minion.AttackDamage == 5 && minion.Health == 6 && spell.Cost == 4, "buff only minions in hand");
    });
}
