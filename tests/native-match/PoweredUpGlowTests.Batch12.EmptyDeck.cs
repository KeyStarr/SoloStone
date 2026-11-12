using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b12-empty-deck", ProofKind.PoweredUp)]
    private static void RunBatch12EmptyDeck()
    {
        Scenario("TIME_443", (f, card, met) => {
            foreach (var held in f.Player.DeckZone.ToArray()) f.Player.SetasideZone.Add(f.Player.DeckZone.Remove(held));
            if (!met) SabberStoneCore.Model.Entities.Entity.FromCard(f.Player, Cards.FromId("CS2_182"), zone: f.Player.DeckZone);
            var enemy = f.Summon("CS2_200", f.Game.Player2); enemy.BaseHealth = 20;
        }, (f, card) => PlayCardTask.Spell(f.Player, card), (f, card) => {
            return f.Game.Player2.BoardZone.Single().Damage == 6;
        });
    }
}
