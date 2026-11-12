using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b12-cthun", ProofKind.PoweredUp)]
    private static void RunBatch12Cthun()
    {
        Scenario("BE_012", (f, card, met) => {
            f.Process(PlayCardTask.Spell(f.Player,f.Draw("BE_071")));
            f.Mana(10);
            if(met) f.Process(PlayCardTask.Spell(f.Player,f.Draw("BE_071")));
            f.Mana(10);
        }, (f,card) => PlayCardTask.Minion(f.Player,card), (f,card) => {
            bool ready = f.Player.Choice != null;
            if(ready) f.Process(ChooseTask.Pick(f.Player,f.Player.Choice.Choices.Single(e => f.Game.IdEntityDic[e].Card.Id == "BE_013t4")));
            Check(f.Player.BoardZone.Count == (ready ? 3 : 1),"Deacon held Shadow gate and base Servants outcome");
            return ready;
        });
        Scenario("BE_014", (f,card,met) => {
            if(met) f.Player.DeckCards.Add(Cards.FromId("BE_013"));
            f.Draw("BE_013");
        }, (f,card) => PlayCardTask.Minion(f.Player,card), (f,card) => {
            bool ready = f.Player.Choice != null;
            if(ready) f.Process(ChooseTask.Pick(f.Player,f.Player.Choice.Choices.Single(e => f.Game.IdEntityDic[e].Card.Id == "BE_013t4")));
            Check(f.Player.BoardZone.Count == (ready ? 3 : 1),"Spawn initial-deck gate excludes merely generated C'Thun");
            return ready;
        });
    }
}
