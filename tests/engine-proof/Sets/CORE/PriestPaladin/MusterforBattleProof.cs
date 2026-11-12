using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz, exact current card text/tags.
// Existing legacy generation and rebalanced powers retained (owner decision, 2026-10-21).
internal static class MusterforBattleProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_GVG_061", (g, p, id) => {
        var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182");
        Play(g, p, id);
        var recruits = p.BoardZone.Where(m => m.Card.Id == "CS2_101t").ToArray();
        Core2026Proof.Check(recruits.Length == 3 && recruits.All(m => m.Card.AssetId == 1652 && m.AttackDamage == 1 && m.Health == 1 && m.Card.Cost == 1), "three exact 1/1 Silver Hand Recruits");
        Core2026Proof.Check(p.BoardZone.Slots.Select(m => m.Id).SequenceEqual(new[] { location.Id, left.Id }.Concat(recruits.Select(m => m.Id))), "spell summons append after physical slots");
        Core2026Proof.Check(p.Hero.Weapon.Card.Id == "CS2_091" && p.Hero.Weapon.Card.AssetId == 383 && p.Hero.Weapon.AttackDamage == 1 && p.Hero.Weapon.Durability == 4, "exact Light's Justice 1/4");
        ParkBoard(p); LocationProofs.Location(p);
        while (p.BoardZone.SlotCount < 6) Summon(p, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == "CS2_101t") == 1, "six occupied slots allow exactly one recruit");
        Play(g, p, "EX1_383t"); Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == "CS2_101t") == 1 && p.Hero.Weapon.Card.Id == "CS2_091", "full board still replaces weapon");
        Core2026Proof.Check(p.Opponent.BoardZone.Count == 0 && p.Opponent.Hero.Weapon == null, "opponent gains nothing");
    }, legacyPoolMember: true);
}
