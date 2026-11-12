using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, exact current card text/tags.
// Existing legacy generation and rebalanced powers retained (owner decision, 2026-10-21).
internal static class FlashHealProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_AT_055", (g, p, id) => {
        p.Hero.Damage = 12; p.Opponent.Hero.Damage = 12;
        Play(g, p, id, p.Hero); Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Hero.Damage == 7 && p.Opponent.Hero.Damage == 7, "heal five to either hero");
        var friend = Summon(p, "CS2_200"); var enemy = Summon(p.Opponent, "CS2_200");
        friend.Damage = 6; enemy.Damage = 3;
        Play(g, p, id, friend); Play(g, p, id, enemy);
        Core2026Proof.Check(friend.Damage == 1 && enemy.Damage == 0, "heal five on either minion, capped at maximum Health");
        Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Health == 7 && p.HandZone.Count == 0, "undamaged target legal, no overheal or draw");
    }, legacyPoolMember: true);
}
