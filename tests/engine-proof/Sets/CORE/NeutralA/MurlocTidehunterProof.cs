using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz (printed rules, keywords and dependencies).
internal static class MurlocTidehunterProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_506", (g, p, id) => {
        var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        var source = PlayAt(g, p, id, 2); var token = p.BoardZone.Single(m => m.Card.Id == "EX1_506a");
        Core2026Proof.Check(token.Card.AssetId == 1078 && token.AttackDamage == 1 && token.Health == 1 && token.IsRace(Race.MURLOC)
            && p.BoardZone.Slots.Select(e => e.Id).SequenceEqual(new[] { location.Id, left.Id, source.Id, token.Id, right.Id }), "exact 1/1 Murloc Scout immediately right of source, physical slots");
        ParkBoard(p); LocationProofs.Location(p); while (p.BoardZone.SlotCount < 5) Summon(p, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == "EX1_506a") == 1, "last free slot receives Scout");
        ParkBoard(p); LocationProofs.Location(p); while (p.BoardZone.SlotCount < 6) Summon(p, "CS2_182");
        PoolPilotProof.Resume(g, next => { Play(next, next.Player1, id);
            Core2026Proof.Check(next.Player1.BoardZone.SlotCount == 7 && next.Player1.BoardZone.All(m => m.Card.Id != "EX1_506a"), "no Scout at full physical board"); });
    }, legacyPoolMember: true);
}
