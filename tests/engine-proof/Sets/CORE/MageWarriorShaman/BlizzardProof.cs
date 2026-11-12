using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class BlizzardProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CS2_028", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); var small = Summon(p.Opponent, "CS2_189");
        Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 2 && enemy.IsFrozen && small.Zone.Type == Zone.GRAVEYARD, "all enemy minions damaged; survivors frozen");
        Core2026Proof.Check(friend.Damage == 0 && !friend.IsFrozen && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "friendly minions and heroes unaffected");
        ParkBoard(p.Opponent); Play(g, p, id); Core2026Proof.Check(p.Opponent.BoardZone.SlotCount == 0, "empty enemy board accepted");
        Summon(p, "CS2_142"); enemy = Summon(p.Opponent, "CS2_182"); Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 3 && enemy.IsFrozen, "Spell Damage applies");
    }, legacyPoolMember: true);
}
