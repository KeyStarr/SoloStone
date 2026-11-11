using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class ExecuteProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CS2_108", (g, p, id) => {
        var healthy = Summon(p.Opponent, "CS2_182"); var damaged = Summon(p.Opponent, "CS2_182"); damaged.Damage = 1;
        var friend = Summon(p, "CS2_182"); friend.Damage = 1;
        var source = Add(p, id); var options = p.Options().Where(t => t is PlayCardTask && t.Source?.Id == source.Id).ToArray();
        Core2026Proof.Check(options.Length > 0 && options.All(t => t.Target?.Id == damaged.Id), "only damaged enemy minion is legal; heroes, friendly and healthy targets excluded");
        PlayEntity(g, p, source, damaged);
        Core2026Proof.Check(damaged.Zone.Type == Zone.GRAVEYARD && healthy.Zone == p.Opponent.BoardZone && friend.Zone == p.BoardZone, "destroy chosen damaged enemy only");
        source = Add(p, id); p.UsedMana = 0;
        Core2026Proof.Check(!p.Options().Any(t => t is PlayCardTask && t.Source?.Id == source.Id), "no damaged enemy means no legal play");
    }, legacyPoolMember: true);
}
