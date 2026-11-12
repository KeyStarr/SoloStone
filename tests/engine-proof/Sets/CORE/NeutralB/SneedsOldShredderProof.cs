using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class SneedsOldShredderProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_GVG_114", (g, p, id) => {
        // Owner exception (2026-10-21): retain existing legacy random pool and RNG recipe.
        Minion source = null; var board = DeathrattleSlotBoard(p, () => source = Summon(p, id));
        PoolPilotProof.Resume(g, next => {
            Kill(next, (Minion)next.IdEntityDic[source.Id]); var owner = next.Player1;
            var result = owner.BoardZone.Single(m => m.Id != board.Left.Id && m.Id != board.Right.Id);
            Core2026Proof.Check(result.Card.Type == CardType.MINION && result.Card.Rarity == Rarity.LEGENDARY && result.Card.Id != id && result.Card.AssetId > 0, "seeded legacy result has exact Legendary minion identity and excludes current source");
            Core2026Proof.Check(result.AttackDamage == result.Card.ATK && result.Health == result.Card.Health, "random summon has selected legacy definition base stats; Battlecry is not played");
            Core2026Proof.Check(owner.BoardZone.GetSlot(2).Id == result.Id && owner.BoardZone.GetSlot(3).Id == board.Right.Id, "random summon fills physical dying slot");
    });
        ParkBoard(p); source = Summon(p, id); source.Silence(); Kill(g, source); Core2026Proof.Check(p.BoardZone.SlotCount == 0, "Silence removes random Deathrattle");
        source = Summon(p, id); while (!p.BoardZone.IsFull) Summon(p, "CS2_182"); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Rarity == Rarity.LEGENDARY) == 1, "full board gets exactly one Legendary in freed slot");
        End(g);
    }, legacyPoolMember: true);
}
