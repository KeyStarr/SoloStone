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
internal static class CoreQuickShotProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_BRM_013", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182"); int deck = p.DeckZone.Count; int top = p.DeckZone.TopCard.Id;
        Play(g, p, id, enemy); Core2026Proof.Check(enemy.Damage == 3 && p.DeckZone.Count == deck - 1 && p.HandZone.Single().Id == top, "empty after cast draws actual top entity");
        Play(g, p, id, p.Hero); Core2026Proof.Check(p.Hero.Damage == 3 && p.DeckZone.Count == deck - 1 && p.HandZone.Count == 1, "nonempty hand does not draw");
        foreach (var c in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(c));
        Summon(p, "CS2_142"); Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 4 && p.DeckZone.Count == deck - 2, "Spell Damage and empty-hand draw together");
        foreach (var c in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(c));
        foreach (var c in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(c));
        int damage = p.Hero.Damage; Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.HandZone.Count == 0 && p.Hero.Damage == damage + 1, "empty deck attempts draw and causes fatigue");
    }, legacyPoolMember: true);
}
