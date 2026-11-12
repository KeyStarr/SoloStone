using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: data/carddb/37.0.0.254477/cards.json.gz (printed rules, keywords and dependencies).
internal static class DirtyRatProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_CFM_790", (g, p, id) => {
        var minion = Add(p.Opponent, "EX1_506"); var second = Add(p.Opponent, "EX1_506"); var spell = Add(p.Opponent, "CS2_029");
        var source = (Minion)Play(g, p, id);
        var summoned = p.Opponent.BoardZone.Single();
        Core2026Proof.Check(source.HasTaunt && (summoned.Id == minion.Id || summoned.Id == second.Id) && p.Opponent.HandZone.Count == 2
            && spell.Zone == p.Opponent.HandZone && summoned.AttackDamage == 2 && summoned.Health == 1 && p.HandZone.Count == 0,
            "pull exactly one of two enemy 2/1 Tidehunters without its Scout Battlecry; leave spell");
        var leftover = p.Opponent.HandZone.Single(c => c.Card.Type == CardType.MINION); p.Opponent.SetasideZone.Add(p.Opponent.HandZone.Remove(leftover));
        Play(g, p, id); Core2026Proof.Check(p.Opponent.BoardZone.Count == 1 && p.Opponent.HandZone.Single().Id == spell.Id, "no minion in hand: nothing summoned");
        var held = Add(p.Opponent, "CS2_182"); var ownHeld = Add(p, "EX1_506");
        LocationProofs.Location(p.Opponent); while (!p.Opponent.BoardZone.IsFull) Summon(p.Opponent, "CS2_182");
        Play(g, p, id); Core2026Proof.Check(held.Zone == p.Opponent.HandZone && ownHeld.Zone == p.HandZone && p.Opponent.BoardZone.SlotCount == 7, "full physical board leaves both hands untouched");
        ParkBoard(p.Opponent); PoolPilotProof.Resume(g, next => {
            Play(next, next.Player1, id);
            Core2026Proof.Check(next.Player2.BoardZone.Single().Id == held.Id && next.IdEntityDic[ownHeld.Id].Zone == next.Player1.HandZone, "only enemy hand recruited in clone/load/original");
        });
    }, legacyPoolMember: true);
}
