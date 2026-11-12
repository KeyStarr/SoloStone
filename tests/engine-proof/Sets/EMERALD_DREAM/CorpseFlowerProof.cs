using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using static Core2026Proof;
using static PoolPilotProof;
using static StandardSetsProof;

internal static class CorpseFlowerProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_815", (g, p, id) => {
        g = GraftedKnightProof.NewGame(g); p = g.Player1;
        var flower = (Minion)Play(g, p, id);
        CorpseRules.Gain(p, 1);
        var enemy = Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(enemy.Damage == 0 && CorpseRules.Available(p) == 1, "insufficient corpses spends nothing and deals nothing");
        CorpseRules.Gain(p, 3);
        var friendly = Summon(p, "CS2_182");
        Core2026Proof.Check(friendly.Damage == 0 && CorpseRules.Available(p) == 4, "friendly summons do not trigger");
        LocationProofs.Location(p.Opponent);
        Core2026Proof.Check(CorpseRules.Available(p) == 4, "enemy location is not a minion summon");
        Resume(g, next => Summon(next.Player2, "CS2_182"));
        Core2026Proof.Check(p.Opponent.BoardZone.Last().Damage == 3 && CorpseRules.Available(p) == 2
            && p[GameTag.CORPSES_SPENT_THIS_GAME] == 2, "enemy summon spends exactly two and deals three after clone/snapshot");
        Summon(p, "CS2_052");
        enemy = Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(enemy.Damage == 3 && CorpseRules.Available(p) == 0, "minion trigger ignores spell damage");
        CorpseRules.Gain(p, 2); flower.Silence(); enemy = Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(enemy.Damage == 0 && CorpseRules.Available(p) == 2, "Silence removes trigger");
        flower.Card.Power.Trigger.SingleTask.Process(g, p, flower, null, new TaskStack());
        Core2026Proof.Check(CorpseRules.Available(p) == 2, "missing target cannot consume corpses");
        LocationProofs.Location(p); GraftedKnightProof.FullBoardRejects(g, id);
        BattlecryBeforeFlower(g);
        CompetingFlowers(g, id);
    });

    private static void BattlecryBeforeFlower(Game seeded)
    {
        var g = GraftedKnightProof.NewGame(seeded); var p = g.Player1;
        Summon(p, "EDR_815"); var victim = Summon(p, "CS2_101t"); CorpseRules.Gain(p, 1);
        End(g);
        Play(g, p.Opponent, "CS2_150", victim); // Stormpike Commando kills victim in its Battlecry.
        Core2026Proof.Check(victim.Zone == p.GraveyardZone && p.Opponent.BoardZone.IsEmpty && CorpseRules.Available(p) == 0
            && p[GameTag.CORPSES_SPENT_THIS_GAME] == 2, "Battlecry death supplies second corpse before after-summon trigger");
    }

    private static void CompetingFlowers(Game seeded, string id)
    {
        var g = GraftedKnightProof.NewGame(seeded); var p = g.Player1;
        Summon(p, id); Summon(p, id); CorpseRules.Gain(p, 3);
        var enemy = Summon(p.Opponent, "CS2_200");
        Core2026Proof.Check(enemy.Damage == 3 && CorpseRules.Available(p) == 1, "two flowers recheck resources; no partial second spend");
        CorpseRules.Gain(p, 3);
        enemy = Summon(p.Opponent, "CS2_200");
        Core2026Proof.Check(enemy.Damage == 6 && CorpseRules.Available(p) == 0, "two funded flowers each deal three");
    }
}
