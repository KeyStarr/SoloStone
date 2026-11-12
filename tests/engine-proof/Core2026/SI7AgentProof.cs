using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class SI7AgentProof
{
    internal static void Run() => Case("CORE_EX1_134", (g, p, id) => {
        Play(g, p, "GAME_005"); var enemy = Summon(p.Opponent, "CS2_182");
        Play(g, p, id, enemy); Core2026Proof.Check(enemy.Damage == 3, "SI:7 combo deals 3");
    });
}
