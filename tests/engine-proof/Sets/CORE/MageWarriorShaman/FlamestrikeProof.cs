using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class FlamestrikeProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CS2_032", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_200"); var small = Summon(p.Opponent, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 5 && small.Zone.Type == Zone.GRAVEYARD, "five damage across enemy board");
        Core2026Proof.Check(friend.Damage == 0 && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "friendly minions and heroes unaffected");
        ParkBoard(p.Opponent); Play(g, p, id); Summon(p, "CS2_142"); enemy = Summon(p.Opponent, "CS2_200");
        Play(g, p, id); Core2026Proof.Check(enemy.Damage == 6 && enemy.Health == 1, "empty board accepted; Spell Damage applies");
    }, legacyPoolMember: true);
}
