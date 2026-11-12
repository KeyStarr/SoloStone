using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned data/carddb/37.0.0.254477/cards.json.gz (identity and current text).
internal static class RavagingGhoulProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_OG_149", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182"); var shield = Summon(p.Opponent, "GVG_085"); Summon(p, "CS2_142");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(friend.Damage == 1 && enemy.Damage == 1 && source.Damage == 0 && shield.Damage == 0 && !shield.HasDivineShield && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0, "one to all other minions, ignores Spell Damage, shield blocks, excludes source and heroes");
        Summon(p, id); Core2026Proof.Check(friend.Damage == 1 && enemy.Damage == 1, "summoning does not execute Battlecry");
        ParkBoard(p); ParkBoard(p.Opponent); source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.Damage == 0 && p.BoardZone.Count == 1, "empty board Battlecry leaves source intact");
    }, legacyPoolMember: true);
}
