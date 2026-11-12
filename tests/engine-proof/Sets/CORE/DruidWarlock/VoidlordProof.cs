using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned 37.0.0.254477 export; owner requests unchanged legacy power (2026-10-21).
internal static class VoidlordProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_LOOT_368", (g, p, id) => {
        Minion source = null; var board = DeathrattleSlotBoard(p, () => source = Summon(p, id)); Core2026Proof.Check(source.HasTaunt, "printed Taunt");
        PoolPilotProof.Resume(g, next => {
            Kill(next, (Minion)next.IdEntityDic[source.Id]); var owner = next.Player1;
            var tokens = owner.BoardZone.Where(m => m.Card.Id == "CS2_065").OrderBy(m => m.Id).ToArray();
            Core2026Proof.Check(tokens.Length == 3 && tokens.All(m => m.Card.AssetId == 48 && m.AttackDamage == 1 && m.Health == 3 && m.HasTaunt && m.IsRace(Race.DEMON)), "three exact 1/3 Taunt Voidwalkers");
            Core2026Proof.Check(owner.BoardZone.Slots.Select(m => m.Id).SequenceEqual(new[]{board.Location.Id,board.Left.Id}.Concat(tokens.Select(m=>m.Id)).Append(board.Right.Id)), "three summons fill physical dying slot rightward");
    });
        ParkBoard(p); source = Summon(p, id); source.Silence(); Kill(g, source); Core2026Proof.Check(p.BoardZone.SlotCount == 0, "Silence removes Deathrattle");
        source = Summon(p, id); for (int i=0;i<5;i++) Summon(p,"CS2_182"); Kill(g, source);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m=>m.Card.Id=="CS2_065")==2, "summon stops at seven slots");
        End(g);
    }, legacyPoolMember: true);
}
