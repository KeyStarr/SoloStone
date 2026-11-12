using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TickingTimebombProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_603", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(friend.Zone.Type == Zone.PLAY && p.Opponent.Hero.Damage == 0, "no enemy minion means no effect");
        var shield = Summon(p.Opponent, "CS2_182"); shield.HasDivineShield = true;
        source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(shield.Zone.Type == Zone.GRAVEYARD && friend.Damage == 0, "destroy bypasses Divine Shield and excludes friendly minions");
        var first = Summon(p.Opponent, "CS2_182"); var second = Summon(p.Opponent, "CS2_182");
        source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(new[] { first, second }.Count(m => m.Zone.Type == Zone.GRAVEYARD) == 1, "exactly one random enemy destroyed");
        source = (Minion)Play(g, p, id); Play(g, p, "BE_228", source); Kill(g, source);
        Core2026Proof.Check(p.Opponent.BoardZone.Count == 1, "silence removes destruction deathrattle");
    });
}
