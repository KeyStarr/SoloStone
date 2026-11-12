using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class FanofKnivesProof
{
    internal static void Run() => Case("CORE_EX1_129", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182"); var friend = Summon(p, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 1 && friend.Damage == 0 && p.HandZone.Count == 1, "Fan damages enemy minions and draws");
    });
}
