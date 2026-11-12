using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class RaidLeaderProof
{
    internal static void Run() => Case("CORE_CS2_122", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(friend.AttackDamage == 5 && source.AttackDamage == 2 && source.Health == 3, "Raid Leader aura excludes itself");
        Kill(g, source); Core2026Proof.Check(friend.AttackDamage == 4, "aura removed on death");
    });
}
