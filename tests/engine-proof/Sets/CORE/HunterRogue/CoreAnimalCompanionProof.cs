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
internal static class CoreAnimalCompanionProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_NEW1_031", (g, p, id) => {
        var seen = new HashSet<string>();
        for (int i = 0; i < 30 && seen.Count < 3; i++)
        {
            ParkBoard(p); var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182");
            var clone = g.CloneExact(history: true); Play(g, p, id); Play(clone, clone.Player1, id); var companion = p.BoardZone.Last();
            Core2026Proof.Check(new[] { "NEW1_032", "NEW1_033", "NEW1_034" }.Contains(companion.Card.Id) && companion.Card.IsRace(Race.BEAST)
                && p.BoardZone.Slots.Select(m => m.Id).SequenceEqual(new[] { location.Id, left.Id, companion.Id }) && Canonical.State(g) == Canonical.State(clone), "one pinned Beast Companion at far right with cloned RNG parity");
            seen.Add(companion.Card.Id);
            switch (companion.Card.Id)
            {
                case "NEW1_032": Core2026Proof.Check(companion.AttackDamage == 4 && companion.Health == 4 && companion.HasTaunt, "Misha 4/4 Taunt"); break;
                case "NEW1_034": Core2026Proof.Check(companion.AttackDamage == 4 && companion.Health == 2 && companion.HasCharge, "Huffer 4/2 Charge"); break;
                case "NEW1_033":
                    Core2026Proof.Check(companion.AttackDamage == 2 && companion.Health == 4 && left.AttackDamage == 5, "Leokk 2/4 and other friendly +1 Attack");
                    Play(g, p, "CORE_SW_066", companion); Core2026Proof.Check(left.AttackDamage == 4, "Silence ends Leokk aura"); break;
            }
        }
        Core2026Proof.Check(seen.Count == 3, "all three RNG outcomes exercised");
        ParkBoard(p); LocationProofs.Location(p); for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        var blocked = Add(p, id); Core2026Proof.Check(!p.Options().Any(t => t is PlayCardTask && t.Source == blocked), "seven physical slots prevent cast");
        Kill(g, p.BoardZone.Last()); PlayEntity(g, p, blocked); Core2026Proof.Check(p.BoardZone.SlotCount == 7, "one free slot admits exactly one Companion");
    }, legacyPoolMember: true);
}
