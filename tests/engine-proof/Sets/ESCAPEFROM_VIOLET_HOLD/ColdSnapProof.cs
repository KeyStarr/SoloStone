using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

internal static class ColdSnapProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_125", (g, p, id) => {
        HandBoundaries(g, p, id, 1);
        var enemy = Summon(p.Opponent, "CS2_200"); var friend = Summon(p, "CS2_182");
        var spell = Add(p, id);
        Core2026Proof.Check(p.Options().Where(t => t.Source?.Id == spell.Id && t.PlayerTaskType == PlayerTaskType.PLAY_CARD)
            .All(t => t.Target?.Controller == p.Opponent), "only enemy targets");
        Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[spell.Id], (Minion)next.IdEntityDic[enemy.Id]));
        Core2026Proof.Check(enemy.IsFrozen && !friend.IsFrozen, "freezes enemy only");
        Generated(g, spell, p.HandZone, 1);
        EmptyHand(p); enemy.IsFrozen = false;
        Pool(g, id, 0, () => {
            Play(g, p, id, enemy);
            Core2026Proof.Check(enemy.IsFrozen && p.HandZone.Count == 0, "empty pool still freezes");
        });
        Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.IsFrozen, "enemy hero can be frozen");
    });
}
