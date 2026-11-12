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
internal static class CoreCallOfTheWildProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_OG_211", (g, p, id) => {
        var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        Play(g, p, id); var huffer = p.BoardZone.Single(m => m.Card.Id == "NEW1_034"); var leokk = p.BoardZone.Single(m => m.Card.Id == "NEW1_033"); var misha = p.BoardZone.Single(m => m.Card.Id == "NEW1_032");
        Core2026Proof.Check(p.BoardZone.Slots.Select(m => m.Id).SequenceEqual(new[] { location.Id, left.Id, huffer.Id, leokk.Id, misha.Id })
            && huffer.AttackDamage == 5 && huffer.Health == 2 && huffer.HasCharge && leokk.AttackDamage == 2 && leokk.Health == 4
            && misha.AttackDamage == 5 && misha.Health == 4 && misha.HasTaunt && left.AttackDamage == 5 && enemy.AttackDamage == 4, "all pinned companions append in legacy order, Leokk buffs only other friendly minions");
        var clone = g.CloneExact(history: true); Kill(g, leokk); Kill(clone, (Minion)clone.IdEntityDic[leokk.Id]);
        Core2026Proof.Check(huffer.AttackDamage == 4 && misha.AttackDamage == 4 && left.AttackDamage == 4 && Canonical.State(g) == Canonical.State(clone), "Leokk leaving removes aura with clone parity");
        ParkBoard(p); LocationProofs.Location(p); for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Last().Card.Id == "NEW1_034", "one free slot summons Huffer only");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.SlotCount == 7, "full board never overflows");
    }, legacyPoolMember: true);
}
