using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, exact current card text/tags.
// Existing legacy generation and rebalanced powers retained (owner decision, 2026-10-21).
internal static class HolyNovaProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CS1_112", (g, p, id) => {
        var friend = Summon(p, "CS2_200"); friend.Damage = 4; p.Hero.Damage = 4;
        var other = Summon(p.Opponent, "CS2_200");
        var enemy = Summon(p.Opponent, "CS2_200"); enemy.Damage = 1; p.Opponent.Hero.Damage = 4;
        Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 3 && other.Damage == 2 && friend.Damage == 2 && p.Hero.Damage == 2 && p.Opponent.Hero.Damage == 4, "two damage to enemy minions; two healing to friendly characters only");
        friend.Damage = 4; p.Hero.Damage = 4;
        Summon(p, "CS2_142"); Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 6 && other.Damage == 5 && friend.Damage == 2 && p.Hero.Damage == 2 && p.Opponent.Hero.Damage == 4, "Spell Damage boosts damage only; healing stays exactly two");
        friend.Damage = 0; p.Hero.Damage = 0;
        ParkBoard(p.Opponent); Play(g, p, id);
        Core2026Proof.Check(p.Hero.Damage == 0 && friend.Health == 7, "empty enemy board and undamaged friends legal");
    }, legacyPoolMember: true);
}
