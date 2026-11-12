using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class MarkoftheWildProof
{
    internal static void Run() => Case("CORE_CS2_009", (g, p, id) => {
        var friend = Summon(p, "CS2_182");
        Play(g, p, id, friend);
        Core2026Proof.Check(friend.AttackDamage == 6 && friend.Health == 8 && friend.HasTaunt, "Mark +2/+3 and Taunt");
        Play(g, p, "CS2_203", friend);
        Core2026Proof.Check(friend.AttackDamage == 4 && friend.Health == 5 && !friend.HasTaunt, "silence removes Mark");
    });
}
