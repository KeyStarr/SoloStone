using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz (printed rules, keywords and dependencies).
internal static class SunfuryProtectorProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_058", (g, p, id) => {
        var far = Summon(p, "CS2_182"); var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        var enemy = Summon(p.Opponent, "CS2_182"); var source = PlayAt(g, p, id, 2);
        Core2026Proof.Check(left.HasTaunt && right.HasTaunt && !far.HasTaunt && !source.HasTaunt && !enemy.HasTaunt, "only two physical neighbours get Taunt");
        Kill(g, source); Core2026Proof.Check(left.HasTaunt && right.HasTaunt, "granted Taunt survives source departure");
        left.Silence(); Core2026Proof.Check(!left.HasTaunt, "recipient Silence removes granted Taunt");
        ParkBoard(p); var location = LocationProofs.Location(p); right = Summon(p, "CS2_182");
        source = PlayAt(g, p, id, 0);
        Core2026Proof.Check(!source.HasTaunt && !right.HasTaunt && location.ZonePosition == 1, "location blocks adjacency, no skipping across it");
        ParkBoard(p); source = (Minion)Play(g, p, id); Core2026Proof.Check(!source.HasTaunt, "alone has no target and no self-Taunt");
    }, legacyPoolMember: true);
}
