using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b12-cost-memory", ProofKind.PoweredUp)]
    private static void RunBatch12CostMemory()
    {
        Scenario("CATA_213", (f, card, met) => {
            f.Player.DeckCards.Clear();
            f.Player.DeckCards.AddRange(Enumerable.Repeat(Cards.FromId("CS2_182"), met ? 25 : 24));
            f.Player.DeckZone.Add(SabberStoneCore.Model.Entities.Entity.FromCard(f.Player, Cards.FromId("CS2_182")));
        }, (f, card) => PlayCardTask.Minion(f.Player, card), (f, card) => {
            return f.Player.DeckZone.OfType<Minion>().Any(m => m.AttackDamage > m.Card.ATK);
        });
    }
}
