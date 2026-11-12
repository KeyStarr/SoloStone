using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class HeadhuntersHatchetProof
{
    internal static void Run() => Case("CORE_TRL_111", (g, p, id) => {
        Summon(p, "CS2_172"); Play(g, p, id); Core2026Proof.Check(p.Hero.Weapon.Durability == 3, "Beast gives Hatchet +1 Durability; actual=" + p.Hero.Weapon.Durability);
    });
}
