using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class SlamProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_391", (g, p, id) => {
        var target = Summon(p.Opponent, "CS2_182"); Play(g, p, id, target);
        Core2026Proof.Check(target.Damage == 2 && p.HandZone.Count == 1, "survivor draws one");
        var small = Summon(p.Opponent, "CS2_172"); Play(g, p, id, small);
        Core2026Proof.Check(small.Zone.Type == Zone.GRAVEYARD && p.HandZone.Count == 1, "lethal damage does not draw");
        var shield = Summon(p, "GVG_085"); Play(g, p, id, shield);
        Core2026Proof.Check(!shield.HasDivineShield && shield.Damage == 0 && p.HandZone.Count == 2, "friendly shielded survivor draws despite prevented damage");
        var source = Add(p, id); p.UsedMana = 0;
        Core2026Proof.Check(p.Options().Where(t => t is PlayCardTask && t.Source?.Id == source.Id).All(t => t.Target is Minion), "heroes excluded");
    }, legacyPoolMember: true);
}
