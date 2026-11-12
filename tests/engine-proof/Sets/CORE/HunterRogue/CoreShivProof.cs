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
internal static class CoreShivProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_278", (g, p, id) => {
        var target = Summon(p.Opponent, "CS2_182"); int deck = p.DeckZone.Count; int top = p.DeckZone.TopCard.Id;
        Play(g, p, id, target); Core2026Proof.Check(target.Damage == 1 && p.HandZone.Single().Id == top && p.DeckZone.Count == deck - 1, "one damage and actual top-deck draw");
        Summon(p, "CS2_142"); Play(g, p, id, p.Hero);
        Core2026Proof.Check(p.Hero.Damage == 2 && p.DeckZone.Count == deck - 2, "friendly hero and Spell Damage");
        var shield = Summon(p.Opponent, "GVG_085"); Play(g, p, id, shield);
        Core2026Proof.Check(shield.Damage == 0 && !shield.HasDivineShield && p.DeckZone.Count == deck - 3, "prevented damage still draws");
        var source = Add(p, id); while (!p.HandZone.IsFull) Add(p, "CS2_182");
        int burned = p.DeckZone.TopCard.Id; PlayEntity(g, p, source, p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.HandZone.Any(c => c.Id == burned) && p.DeckZone.Count == deck - 4, "playing from full hand frees draw slot");
    }, legacyPoolMember: true);
}
