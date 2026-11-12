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
internal static class VoltaicBurstProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_BOT_451", (g, p, id) => {
        var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        Play(g, p, id); var sparks = p.BoardZone.Where(m => m.Card.Id == "BOT_102t").ToArray();
        Core2026Proof.Check(sparks.Length == 2 && sparks.All(m => m.AttackDamage == 1 && m.Health == 1 && m.Cost == 1 && m.IsRush && m.IsRace(Race.ELEMENTAL)), "two exact 1/1 Rush Sparks");
        Core2026Proof.Check(p.BoardZone.Slots.Select(x => x.Id).SequenceEqual(new[] { location.Id, left.Id }.Concat(sparks.Select(x => x.Id))), "spell summons append after physical slots");
        Core2026Proof.Check(sparks.All(m => p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == m.Id && t.Target?.Id == enemy.Id)
            && !p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == m.Id && t.Target is Hero)), "Rush attacks minions immediately, never heroes");
        Core2026Proof.Check(p.OverloadOwed == 1, "one Overload owed");
        End(g); End(g); Core2026Proof.Check(p.OverloadLocked == 1 && p.OverloadOwed == 0 && p.Opponent.OverloadLocked == 0, "only caster locks one mana next turn");
        End(g); End(g); Core2026Proof.Check(p.OverloadLocked == 0, "mana unlocks on following turn");
        ParkBoard(p); LocationProofs.Location(p); while (p.BoardZone.SlotCount < 6) Summon(p, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == "BOT_102t") == 1, "one free physical slot summons one Spark");
        var blocked = Add(p, id); p.UsedMana = 0;
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == blocked.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD), "full board rejects spell");
    }, legacyPoolMember: true);
}
