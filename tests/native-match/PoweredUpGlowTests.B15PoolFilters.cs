using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b15-pool-filters", ProofKind.PoweredUp)]
    private static void RunB15PoolFilters()
    {
        Scenario("DINO_427", (f, card, met) => {
            if (met) f.Process(PlayCardTask.Spell(f.Player, f.Draw("GAME_005")));
        }, (f, card) => PlayCardTask.Minion(f.Player, card),
            (f, card) => f.Player.HandZone.Any(c => c.Card.Id.StartsWith("DINO_") && c.Cost == c.Card.Cost - 2));
        Scenario("CORE_WW_374", (f, card, met) => {
            Check(f.Game.EnableCorpses, "native fixture enables Corpses");
            if (met) CorpseRules.Gain(f.Player, 1);
        }, (f, card) => PlayCardTask.Spell(f.Player, card),
            (f, card) => f.Player[GameTag.CORPSES_SPENT_THIS_GAME] == 1);
        Scenario("EDR_888", (f, card, met) => {
            f.Player[GameTag.IMBUE] = met ? 4 : 3;
        }, (f, card) => PlayCardTask.Minion(f.Player, card), (f, card) => {
            var offered = f.Player.Choice.Choices.ToArray();
            f.Process(ChooseTask.Pick(f.Player, offered[0]));
            return f.Game.IdEntityDic[offered[0]].Cost == 1;
        });
    }
}
