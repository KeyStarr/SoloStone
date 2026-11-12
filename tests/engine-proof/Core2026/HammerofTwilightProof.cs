using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class HammerofTwilightProof
{
    internal static void Run() => Case("CORE_OG_031", (g, p, id) => {
        Play(g, p, id); Play(g, p, "CS2_082");
        Core2026Proof.Check(p.BoardZone.Single().AttackDamage == 4 && p.BoardZone.Single().Health == 2 && p.BoardZone.Single().IsRace(Race.ELEMENTAL), "Hammer summons 4/2 Elemental");
    });
}
