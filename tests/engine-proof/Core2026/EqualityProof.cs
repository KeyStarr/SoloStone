using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class EqualityProof
{
    internal static void Run() => Case("CORE_EX1_619", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(friend.Health == 1 && enemy.Health == 1, "Equality sets all minion health to 1");
    });
}
