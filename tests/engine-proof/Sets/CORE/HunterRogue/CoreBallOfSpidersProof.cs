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
internal static class CoreBallOfSpidersProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_AT_062", (g, p, id) => {
        var webspinner = Cards.FromId("CORE_FP1_011");
        Core2026Proof.Check(webspinner.AssetId == 69692 && webspinner.Cost == 1 && webspinner.Type == CardType.MINION
            && webspinner.Text == "<b>Deathrattle:</b> Get a\nrandom Beast.", "pinned Webspinner dependency identity, cost and exact text");
        var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182");
        Play(g, p, id); var spiders = p.BoardZone.Where(m => m.Card.Id == "CORE_FP1_011").ToArray();
        Core2026Proof.Check(spiders.Length == 3 && spiders.All(m => m.AttackDamage == 1 && m.Health == 1 && m.Card.Deathrattle && m.Card.IsRace(Race.BEAST))
            && p.BoardZone.Slots.Select(m => m.Id).SequenceEqual(new[] { location.Id, left.Id }.Concat(spiders.Select(m => m.Id))), "three pinned 1/1 Beast Webspinners append after physical board slots");
        var clone = g.CloneExact(history: true);
        foreach (var spider in spiders) {
            var before = p.HandZone.Select(c => c.Id).ToHashSet();
            Kill(g, spider); Kill(clone, (Minion)clone.IdEntityDic[spider.Id]);
            CorePoolProof.Generated(g, p, "CORE_FP1_011", p.HandZone.Single(c => !before.Contains(c.Id)));
        }
        Core2026Proof.Check(p.HandZone.Count == 3 && p.HandZone.All(c => c.Card.IsRace(Race.BEAST)) && Canonical.State(g) == Canonical.State(clone), "each actual death generates Beast with cloned RNG parity");
        ParkBoard(p); LocationProofs.Location(p); for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        Play(g, p, id); var single = p.BoardZone.Last(); Core2026Proof.Check(p.BoardZone.SlotCount == 7 && single.Card.Id == "CORE_FP1_011", "one free physical slot limits summons to one");
        var blocked = Add(p, id); Core2026Proof.Check(!p.Options().Any(t => t is PlayCardTask && t.Source == blocked), "full board prevents cast");
        int hand = p.HandZone.Count; single.Silence(); Kill(g, single);
        Core2026Proof.Check(p.HandZone.Count == hand, "Silenced Webspinner has no Deathrattle");
        ParkBoard(p); Play(g, p, id); while (!p.HandZone.IsFull) Add(p, "CS2_182");
        foreach (var spider in p.BoardZone.ToArray()) Kill(g, spider);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.BoardZone.Count == 0, "full hand never overflows on Webspinner deaths");
    }, legacyPoolMember: true);
}
