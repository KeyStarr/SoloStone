using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class HolySmiteProof
{
    internal static void Run() => Case("CORE_CS1_130", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182");
        var source = Add(p, id);
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == source.Id && t.Target is Hero), "Smite cannot target heroes");
        PlayEntity(g, p, source, enemy);
        Core2026Proof.Check(enemy.Damage == 3, "Smite deals 3");
    });
}
