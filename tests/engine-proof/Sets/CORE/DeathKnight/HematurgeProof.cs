using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static DeathKnightC1Proof;
using static PoolPilotProof;

internal static class HematurgeProof
{
    internal static void Run() => DeathKnightC1Proof.Case("CORE_RLK_066", (g, p, id) => {
        DecisionChoiceReplayProof.Battlecry(g.Seed, id, CardClass.MAGE);
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(CorpseRules.Available(p) == 0 && p.Choice == null, "no corpse: Battlecry does nothing");
        CorpseRules.Gain(p, 2);
        Pool(g, id, 0, () => {
            Resolve(g, source);
            Core2026Proof.Check(CorpseRules.Available(p) == 1 && p.Choice == null, "empty admitted pool still spends one, no choice");
        });
        Resolve(g, source);
        Core2026Proof.Check(CorpseRules.Available(p) == 0 && p[GameTag.CORPSES_SPENT_THIS_GAME] == 2
            && p.Choice == null && p.HandZone.Single().Card.Tags.GetValueOrDefault((GameTag)2196) > 0, "indirect one corpse buys Blood-rune card");
        Generated(g, source, p.HandZone, 1);
        CorpseRules.Gain(p, 1); FillHand(p);
        Pool(g, id, 1, () => {
            Resolve(g, source);
            Core2026Proof.Check(p.Choice == null && !g.Resolution.HasSuspendedEffect, "single indirect offer completes inline");
            Core2026Proof.Check(p.HandZone.Count == 10 && CorpseRules.Available(p) == 0, "full hand still spends and resolves without overflow");
        });
        var stolen = NewGame(g.Seed, CardClass.MAGE); var mage = stolen.Player1;
        CorpseRules.Gain(mage, 1); source = (Minion)Play(stolen, mage, id);
        Core2026Proof.Check(mage.Choice != null && mage.Choice.Choices.All(x => stolen.IdEntityDic[x].Card.Tags.GetValueOrDefault((GameTag)2196) > 0), "stolen Hematurge ignores controller class");
        PoolPilotProof.Resume(stolen, next => CoreCostProof.Answer(next)); Generated(stolen, source, mage.HandZone, 1);
        var full = NewGame(g.Seed); FullBoard(full.Player1); CorpseRules.Gain(full.Player1, 1);
        var blocked = Add(full.Player1, id);
        Core2026Proof.Check(!HasPlay(full.Player1, blocked) && CorpseRules.Available(full.Player1) == 1, "mixed full board rejects play without spending");
    });
}
