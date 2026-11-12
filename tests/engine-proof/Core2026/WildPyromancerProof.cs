using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class WildPyromancerProof
{
    internal static void Run() => Case("CORE_NEW1_020", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182"); var source = (Minion)Play(g, p, id);
        Play(g, p, "GAME_005"); Core2026Proof.Check(source.Damage == 1 && enemy.Damage == 1 && p.Opponent.Hero.Damage == 0, "Pyromancer damages all minions after spell");
    });
}
