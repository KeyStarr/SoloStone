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
internal static class CoreJeweledMacawProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_UNG_912", (g, p, id) => {
        CorePoolProof.CrossClass(g, id);
        var clone = g.CloneExact(history: true); var source = Play(g, p, id); Play(clone, clone.Player1, id);
        Core2026Proof.Check(p.HandZone.Count == 1 && p.HandZone.Single().Card.IsRace(Race.BEAST) && Canonical.State(g) == Canonical.State(clone), "Battlecry generates one Beast with cloned RNG parity");
        int count = p.HandZone.Count; Summon(p, id); Core2026Proof.Check(p.HandZone.Count == count, "summoned Macaw has no Battlecry");
        var fullSource = Add(p, id); while (!p.HandZone.IsFull) Add(p, "CS2_182");
        var kept = p.HandZone.Where(c => c != fullSource).Select(c => c.Id).ToHashSet(); PlayEntity(g, p, fullSource);
        Core2026Proof.Check(p.HandZone.Count == 10 && kept.IsSubsetOf(p.HandZone.Select(c => c.Id)) && p.HandZone.Where(c => !kept.Contains(c.Id)).Single().Card.IsRace(Race.BEAST), "full hand play fills freed slot with Beast");
        Summon(p, "LOE_077"); p.SetasideZone.Add(p.HandZone.Remove(p.HandZone[0])); var doubled = Add(p, id);
        var keptFull = p.HandZone.Where(c => c != doubled).Select(c => c.Id).ToHashSet(); PlayEntity(g, p, doubled);
        Core2026Proof.Check(p.HandZone.Count == 10 && keptFull.IsSubsetOf(p.HandZone.Select(c => c.Id)), "double Battlecry burns excess Beast at hand cap");
    }, legacyPoolMember: true);
}
