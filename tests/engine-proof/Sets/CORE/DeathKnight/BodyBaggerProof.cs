using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static DeathKnightC1Proof;
using static PoolPilotProof;

internal static class BodyBaggerProof
{
    internal static void Run() => DeathKnightC1Proof.Case("RLK_503", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.AttackDamage == 1 && source.Health == 3 && source.IsRace(Race.UNDEAD)
            && CorpseRules.Available(p) == 1 && CorpseRules.Available(p.Opponent) == 0, "Battlecry gains one friendly corpse");
        Resume(g, next => Kill(next, (Minion)next.IdEntityDic[source.Id]));
        Core2026Proof.Check(CorpseRules.Available(p) == 2, "later death separately gains one, persisted through clone/snapshot");
        FullBoard(p);
        Core2026Proof.Check(!HasPlay(p, Add(p, id)) && CorpseRules.Available(p) == 2, "mixed full board rejects play without gain");
    });
}
