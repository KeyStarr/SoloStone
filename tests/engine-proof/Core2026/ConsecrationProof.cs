using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class ConsecrationProof
{
    internal static void Run() => Case("CORE_CS2_093", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 2 && p.Opponent.Hero.Damage == 2 && friend.Damage == 0 && p.Hero.Damage == 0, "Consecration hits enemies");
    });
}
