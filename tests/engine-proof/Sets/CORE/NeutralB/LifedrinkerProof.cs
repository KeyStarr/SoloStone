using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, current text/tags; unchanged legacy power.
internal static class LifedrinkerProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_GIL_622", (g, p, id) => {
        p.Hero.Damage = 7; p.Opponent.Hero.Armor = 2;
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsRace(Race.BEAST) && p.Hero.Damage == 4 && p.Opponent.Hero.Armor == 0 && p.Opponent.Hero.Damage == 1, "damage enemy hero, heal own hero three");
        Core2026Proof.Check(friend.Damage == 0 && enemy.Damage == 0 && source.Damage == 0, "minions unaffected");
        p.Hero.Damage = 1; p.Opponent.Hero.IsImmune = true; Play(g, p, id);
        Core2026Proof.Check(p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 1, "healing independent of prevented damage and capped at full Health");
        p.Opponent.Hero.IsImmune = false; p.Hero.Damage = 6;
        Summon(p, id); Core2026Proof.Check(p.Opponent.Hero.Damage == 1 && p.Hero.Damage == 6, "summon does not run Battlecry");
    }, legacyPoolMember: true);
}
