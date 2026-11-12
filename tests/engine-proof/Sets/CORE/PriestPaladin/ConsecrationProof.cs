using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, exact current card text/tags.
// Existing legacy generation and rebalanced powers retained (owner decision, 2026-10-21).
internal static class ConsecrationProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CS2_093", (g, p, id) => {
        var friend = Summon(p, "CS2_200"); var enemy = Summon(p.Opponent, "CS2_200");
        var other = Summon(p.Opponent, "CS2_200");
        Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 2 && other.Damage == 2 && p.Opponent.Hero.Damage == 2 && friend.Damage == 0 && p.Hero.Damage == 0, "two damage to all enemies only");
        Summon(p, "CS2_142"); Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 5 && other.Damage == 5 && p.Opponent.Hero.Damage == 5 && friend.Damage == 0 && p.Hero.Damage == 0, "Spell Damage scales both enemy character types");
        ParkBoard(p.Opponent); Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 8, "empty enemy board still damages hero");
    }, legacyPoolMember: true);
}
