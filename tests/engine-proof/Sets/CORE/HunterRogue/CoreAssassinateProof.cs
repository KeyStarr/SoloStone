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
internal static class CoreAssassinateProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CS2_076", (g, p, id) => {
        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        TargetRules(g, p, id, enemy, friendly, enemyOnly: true);
        enemy.HasDivineShield = true; Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Zone == p.Opponent.GraveyardZone && friendly.Zone == p.BoardZone, "destroy bypasses shield and leaves friendly minion");
        var source = Add(p, id); Core2026Proof.Check(!p.Options().Any(t => t is PlayCardTask && t.Source == source), "no enemy minion means no legal play");
    }, legacyPoolMember: true);
}
