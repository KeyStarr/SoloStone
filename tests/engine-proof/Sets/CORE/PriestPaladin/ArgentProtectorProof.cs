using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, exact current card text/tags.
// Existing legacy generation and rebalanced powers retained (owner decision, 2026-10-21).
internal static class ArgentProtectorProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_362", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var source = Add(p, id);
        var options = p.Options().Where(t => t.Source?.Id == source.Id).ToArray();
        Core2026Proof.Check(options.Length > 0 && options.All(t => t.Target?.Id == friend.Id), "only another friendly minion target allowed");
        PlayEntity(g, p, source, friend);
        Core2026Proof.Check(friend.HasDivineShield && !enemy.HasDivineShield && !((Minion)source).HasDivineShield, "shield given only to chosen friend");
        Play(g, p, "CS2_008", friend); Core2026Proof.Check(!friend.HasDivineShield && friend.Damage == 0, "shield prevents first damage");
        Play(g, p, id, friend); friend.Silence(); Core2026Proof.Check(!friend.HasDivineShield, "Silence removes granted shield");
        ParkBoard(p); Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Single().Card.Id == id && !p.BoardZone.Single().HasDivineShield, "no available friendly target: battlecry optional, cannot target self");
    }, legacyPoolMember: true);
}
