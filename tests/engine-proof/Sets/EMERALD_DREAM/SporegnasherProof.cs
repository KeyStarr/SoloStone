using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SporegnasherProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_110", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); Summon(p, "CS2_197");
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.Poisonous, "native Poisonous"); Kill(g, source);
        Core2026Proof.Check(friend.Damage == 0 && p.Opponent.Hero.Damage == 0, "no enemy minions never damages heroes or friends");
        var enemy = Summon(p.Opponent, "CS2_182"); source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && enemy.Damage == 1, "deathrattle deals one damage with Poisonous; spell damage ignored");
        var shield = Summon(p.Opponent, "CS2_182"); shield.HasDivineShield = true;
        source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(shield.Zone.Type == Zone.PLAY && !shield.HasDivineShield && shield.Damage == 0, "shield prevents damage and poison kill");
        var other = Summon(p.Opponent, "CS2_182"); source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(new[] { shield, other }.Count(m => m.Zone.Type == Zone.GRAVEYARD) == 1, "random deathrattle hits exactly one enemy");
        source = (Minion)Play(g, p, id); Play(g, p, "BE_228", source); Kill(g, source);
        Core2026Proof.Check(p.Opponent.BoardZone.Count == 1, "silence removes deathrattle and Poisonous");
    });
}
