using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class FlameImpProof
{
    internal static void Run() => Case("CORE_EX1_319", (g, p, id) => {
        Play(g, p, id); Core2026Proof.Check(p.Hero.Damage == 3, "Flame Imp damages own hero 3");
    });
}
