using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class PrimordialDrakeProof
{
    internal static void Run() => Case("CORE_UNG_848", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(friend.Damage == 2 && enemy.Damage == 2 && source.Damage == 0 && source.HasTaunt, "Drake damages other minions only");
    });
}
