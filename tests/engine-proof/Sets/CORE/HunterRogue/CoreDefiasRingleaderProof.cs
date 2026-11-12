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
internal static class CoreDefiasRingleaderProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_131", (g, p, id) => {
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Count == 1 && !p.BoardZone.Any(m => m.Card.Id == "EX1_131t"), "without Combo summons no Bandit");
        ParkBoard(p); var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        Play(g, p, "GAME_005"); var source = PlayAt(g, p, id, 2); var bandit = p.BoardZone.Single(m => m.Card.Id == "EX1_131t");
        Core2026Proof.Check(bandit.AttackDamage == 2 && bandit.Health == 1 && bandit.Card.Cost == 1
            && p.BoardZone.Slots.Select(m => m.Id).SequenceEqual(new[] { location.Id, left.Id, source.Id, bandit.Id, right.Id }), "Combo summons pinned 2/1 Bandit immediately right of source, counting location");
        Kill(g, source); Core2026Proof.Check(bandit.Zone == p.BoardZone && bandit.AttackDamage == 2, "summoned Bandit survives source leaving");
        ParkBoard(p); LocationProofs.Location(p); for (int i = 0; i < 5; i++) Summon(p, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.SlotCount == 7 && !p.BoardZone.Any(m => m.Card.Id == "EX1_131t"), "playing source into final slot cannot summon Bandit");
    }, legacyPoolMember: true);
}
