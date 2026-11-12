using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class HolyNovaProof
{
    internal static void Run() => Case("CORE_CS1_112", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); friend.Damage = 3; p.Hero.Damage = 3;
        var enemy = Summon(p.Opponent, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 2 && p.Opponent.Hero.Damage == 0 && p.Hero.Damage == 1 && friend.Damage == 1, "Nova damages enemy minions only, heals friends");
    });
}
