using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class ShadowAscendantProof
{
    internal static void Run() => Case("CORE_ICC_210", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var source = (Minion)Play(g, p, id);
        End(g); Core2026Proof.Check(friend.AttackDamage == 5 && friend.Health == 6 && source.AttackDamage == 2, "Ascendant buffs another minion at turn end");
    });
}
