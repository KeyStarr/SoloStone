using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned data/carddb/37.0.0.254477/cards.json.gz (identity and current text).
internal static class HexProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_246", (g, p, id) => {
        var location = LocationProofs.Location(p.Opponent);
        var enemy = Summon(p.Opponent, "EX1_096"); enemy.HasDivineShield = true;
        var friend = Summon(p, "CS2_182"); TargetRules(g, p, id, enemy, friend, false);
        Play(g, p, id, enemy); var frog = p.Opponent.BoardZone.Single();
        Core2026Proof.Check(frog.Card.Id == "hexfrog" && frog.AttackDamage == 0 && frog.Health == 1 && frog.HasTaunt && frog.IsRace(Race.BEAST) && frog.ZonePosition == 1 && p.Opponent.BoardZone.GetSlot(0).Id == location.Id && !frog.HasDivineShield && !frog.HasDeathrattle && (frog.AppliedEnchantments?.Count ?? 0) == 0,
            "exact unbuffed 0/1 Taunt Frog replaces enemy without Deathrattle");
        Core2026Proof.Check(p.Opponent.HandZone.Count == 0, "transform does not execute Loot Hoarder Deathrattle");
        Play(g, p, id, friend); frog = p.BoardZone.Single();
        Core2026Proof.Check(frog.Card.Id == "hexfrog" && frog.Health == 1 && frog.AttackDamage == 0 && frog.HasTaunt, "friendly transformation uses same token");
        while (!p.Opponent.BoardZone.IsFull) Summon(p.Opponent, "CS2_182");
        Play(g, p, id, p.Opponent.BoardZone.First()); Core2026Proof.Check(p.Opponent.BoardZone.SlotCount == 7, "transform works with all seven physical slots occupied");
        ParkBoard(p); ParkBoard(p.Opponent); var empty = Add(p, id); p.UsedMana = 0;
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == empty.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD), "no minion means no legal target");
    }, legacyPoolMember: true);
}
