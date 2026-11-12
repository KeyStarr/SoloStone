using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier C: https://hearthstone.wiki.gg/wiki/Reborn-related, rebirth removes enchantments and the Reborn keyword.
// Tier D: data/carddb/37.0.0.254477/cards.json.gz, current text/tags; unchanged legacy power.
internal static class MurmyProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_ULD_723", (g, p, id) => {
        Minion source = null;
        var board = DeathrattleSlotBoard(p, () => source = PlayAt(g, p, id, 2));
        Core2026Proof.Check(source.AttackDamage == 1 && source.Health == 1 && source[GameTag.REBORN] == 1
            && source.IsRace(Race.MURLOC) && source.IsRace(Race.UNDEAD), "1/1 dual-type Reborn");
        Play(g, p, "CS2_087", source);
        PoolPilotProof.Resume(g, next => {
            Kill(next, (Minion)next.IdEntityDic[source.Id]);
            var reborn = next.Player1.BoardZone.Single(m => m.Card.Id == id);
            Core2026Proof.Check(reborn.Id != source.Id && reborn.AttackDamage == 1 && reborn.Health == 1
                && reborn[GameTag.REBORN] == 0 && (reborn.AppliedEnchantments?.Count ?? 0) == 0, "reborn fresh printed 1/1 without buff or second Reborn");
            Core2026Proof.Check(next.Player1.BoardZone.Slots.Select(e => e.Id).SequenceEqual(new[] { board.Location.Id, board.Left.Id, reborn.Id, board.Right.Id }), "Reborn retains dying physical slot with location");
            Kill(next, reborn); Core2026Proof.Check(!next.Player1.BoardZone.Any(m => m.Card.Id == id), "Reborn only once");
        });
        ParkBoard(p); source = (Minion)Play(g, p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.Count == 0, "Silence prevents Reborn");
        source = (Minion)Play(g, p, id); for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        Play(g, p, "CS2_008", source); Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == id) == 1, "death frees one slot on full seven-slot board");
    }, legacyPoolMember: true);
}
