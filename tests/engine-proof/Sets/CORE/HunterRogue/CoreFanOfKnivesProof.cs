using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned data/carddb/37.0.0.254477/cards.json.gz; current text and printed tags.
internal static class CoreFanOfKnivesProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_129", (g, p, id) => {
        var a = Summon(p.Opponent, "CS2_182"); var b = Summon(p.Opponent, "CS2_200"); var friend = Summon(p, "CS2_182");
        int deck = p.DeckZone.Count; Play(g, p, id);
        Core2026Proof.Check(a.Damage == 1 && b.Damage == 1 && friend.Damage == 0 && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0
            && p.DeckZone.Count == deck - 1 && p.HandZone.Count == 1, "one to every enemy minion only and one draw");
        Summon(p, "CS2_142"); Play(g, p, id); Core2026Proof.Check(a.Damage == 3 && b.Damage == 3 && friend.Damage == 0, "Spell Damage applies to all enemy minions");
        ParkBoard(p.Opponent); Play(g, p, id); Core2026Proof.Check(p.DeckZone.Count == deck - 3 && p.HandZone.Count == 3, "empty enemy board still draws");
    }, legacyPoolMember: true);
}
