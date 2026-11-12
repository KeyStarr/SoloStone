using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class FireElementalProof
{
    internal static void Run() => Case("CORE_CS2_042", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182"); Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Damage == 4, "Fire Elemental deals 4");
    });
}
