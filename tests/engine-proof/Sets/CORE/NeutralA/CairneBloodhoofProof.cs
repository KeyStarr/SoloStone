using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class CairneBloodhoofProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_110", (g, p, id) => {
        Minion source = null; var board = DeathrattleSlotBoard(p, () => source = Summon(p, id));
        Core2026Proof.Check(source.HasTaunt && source.HasDeathrattle, "printed Taunt and Deathrattle");
        PoolPilotProof.Resume(g, next => {
            Kill(next, (Minion)next.IdEntityDic[source.Id]); var owner = next.Player1;
            var token = owner.BoardZone.Single(m => m.Card.Id == "EX1_110t");
            Core2026Proof.Check(token.Card.AssetId == 318 && token.AttackDamage == 5 && token.Health == 5 && !token.HasTaunt, "exact pinned current Baine 5/5 without Taunt");
            Core2026Proof.Check(owner.BoardZone.GetSlot(2).Id == token.Id && owner.BoardZone.GetSlot(3).Id == board.Right.Id, "Baine fills dying physical slot with location counted");
            token.Silence(); Core2026Proof.Check(token.AttackDamage == 5 && token.Health == 5, "Baine base stats survive Silence");
    });
        ParkBoard(p); source = Summon(p, id); source.Silence(); Kill(g, source); Core2026Proof.Check(p.BoardZone.SlotCount == 0, "Silence removes Cairne Deathrattle");
        source = Summon(p, id); while (!p.BoardZone.IsFull) Summon(p, "CS2_182"); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == "EX1_110t") == 1, "full board uses only newly freed slot");
        End(g);
    }, legacyPoolMember: true);
}
