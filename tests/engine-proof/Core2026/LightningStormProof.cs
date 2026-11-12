using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class LightningStormProof
{
    internal static void Run() => Case("CORE_EX1_259", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182"); var friend = Summon(p, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 3 && friend.Damage == 0 && p.OverloadOwed == 1, "Storm deals fixed 3, overloads 1");
    });
}
