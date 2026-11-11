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
internal static class CoreSwashburglarProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_KAR_069", (g, p, id) => {
        CorePoolProof.CrossClass(g, id);
        var clone = g.CloneExact(history: true); var source = Play(g, p, id); Play(clone, clone.Player1, id);
        var generated = p.HandZone.Single(); Core2026Proof.Check(generated.Card.Class != CardClass.NEUTRAL && generated.Card.Class != p.HeroClass && !generated.Card.IsQuest
            && generated[GameTag.DISPLAYED_CREATOR] == source.Id && Canonical.State(g) == Canonical.State(clone), "Battlecry generates one other-class non-Quest card with cloned RNG parity and creator");
        int count = p.HandZone.Count; Summon(p, id); Core2026Proof.Check(p.HandZone.Count == count, "summoning without Battlecry generates nothing");
        var fullSource = Add(p, id); while (!p.HandZone.IsFull) Add(p, "CS2_182");
        var kept = p.HandZone.Where(c => c != fullSource).Select(c => c.Id).ToHashSet(); PlayEntity(g, p, fullSource);
        Core2026Proof.Check(p.HandZone.Count == 10 && kept.IsSubsetOf(p.HandZone.Select(c => c.Id)), "play from full hand uses freed slot, preserves existing cards");
        var brann = Summon(p, "LOE_077"); p.SetasideZone.Add(p.HandZone.Remove(p.HandZone[0]));
        var doubled = Add(p, id); var keptFull = p.HandZone.Where(c => c != doubled).Select(c => c.Id).ToHashSet();
        PlayEntity(g, p, doubled); Core2026Proof.Check(p.HandZone.Count == 10 && keptFull.IsSubsetOf(p.HandZone.Select(c => c.Id)), "doubled Battlecry at hand cap burns excess generation");
    }, legacyPoolMember: true);
}
