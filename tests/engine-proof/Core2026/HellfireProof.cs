using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class HellfireProof
{
    internal static void Run() => Case("CORE_CS2_062", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(friend.Damage == 3 && enemy.Damage == 3 && p.Hero.Damage == 3 && p.Opponent.Hero.Damage == 3, "Hellfire hits all characters");
    });
}
