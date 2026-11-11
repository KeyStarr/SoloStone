using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class SilvermoonPortalProof
{
    internal static void Run() => Case("CORE_KAR_077", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); Play(g, p, id, friend);
        Core2026Proof.Check(friend.AttackDamage == 6 && friend.Health == 7 && p.BoardZone.Single(m => m.Id != friend.Id).Card.Cost == 2, "Portal buffs and summons 2-Cost minion");
    });
}
