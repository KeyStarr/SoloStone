using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class SpikeridgedSteedProof
{
    internal static void Run() => Case("CORE_UNG_952", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); Play(g, p, id, friend);
        Core2026Proof.Check(friend.AttackDamage == 6 && friend.Health == 11 && friend.HasTaunt, "Steed gives +2/+6 Taunt");
        Kill(g, friend); Core2026Proof.Check(p.BoardZone.Single().Card.Id == "UNG_810", "Steed deathrattle summons Stegodon; actual=" + p.BoardZone.Single().Card.Id);
    });
}
