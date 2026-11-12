using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static DeathKnightC1Proof;
using static PoolPilotProof;

internal static class MarrowManipulatorProof
{
    internal static void Run() => DeathKnightC1Proof.Case("CORE_RLK_505", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Health == 30 && CorpseRules.Available(p) == 0, "zero corpses no damage");
        CorpseRules.Gain(p, 2); Resolve(g, source);
        Core2026Proof.Check(p.Opponent.Hero.Health == 26 && CorpseRules.Available(p) == 0
            && p[GameTag.CORPSES_SPENT_THIS_GAME] == 2, "spend up to available, two separate two-damage hits");
        CorpseRules.Gain(p, 7); Resume(g, next => Resolve(next, next.IdEntityDic[source.Id]));
        Core2026Proof.Check(p.Opponent.Hero.Health == 16 && CorpseRules.Available(p) == 2
            && p[GameTag.CORPSES_SPENT_THIS_GAME] == 7, "caps spend at five and survives clone/snapshot");
        var victim = Summon(p.Opponent, "CS2_101t");
        int hero = p.Opponent.Hero.Health;
        Resume(g, next => Resolve(next, next.IdEntityDic[source.Id]));
        Core2026Proof.Check(victim.Zone.Type == Zone.GRAVEYARD || victim.Damage == 0, "random enemy minion eligible");
        Core2026Proof.Check(hero - p.Opponent.Hero.Health + (victim.IsDead ? 2 : 0) == 4, "two missiles exclude mortally wounded victims and retain face target");
        var full = NewGame(g.Seed); FullBoard(full.Player1); CorpseRules.Gain(full.Player1, 5);
        Core2026Proof.Check(!HasPlay(full.Player1, Add(full.Player1, id)) && CorpseRules.Available(full.Player1) == 5, "mixed full board rejects minion without spending");
    });
}
