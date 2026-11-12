using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b12-imp", ProofKind.PoweredUp)]
    private static void RunBatch12Imp()
    {
        Scenario("CAP_401", (f, card, met) => {
            // Friendly deck and enemy hand/board Imp-formants cannot satisfy the enemy-deck condition.
            f.Player.DeckZone.Add(SabberStoneCore.Model.Entities.Entity.FromCard(f.Player, Cards.FromId("CAP_400t2t")));
            f.Summon("CAP_400t2t", f.Game.Player2);
            SabberStoneCore.Actions.Generic.DrawCard(f.Game.Player2, Cards.FromId("CAP_400t2t"));
            if (met) f.Game.Player2.DeckZone.Add(SabberStoneCore.Model.Entities.Entity.FromCard(f.Game.Player2, Cards.FromId("CAP_400t2t")));
            f.Game.Player2.DeckZone.PutOnTop(f.Game.Player2.DeckZone.First(c => c.Card.Id != "CAP_400t2t"));
        }, (f, card) => PlayCardTask.Minion(f.Player, card), (f, card) => {
            var top = f.Game.Player2.DeckZone.TopCard;
            bool moved = top.Card.Id == "CAP_400t2t";
            Check(!moved || (top is Minion imp && imp.AttackDamage == 5 && imp.Health == 5
                && imp.AppliedEnchantments.Any(e => e.Card.Id == "CAP_401e")),
                "Constable moves and buffs the enemy-deck Imp-formant");
            return moved;
        });
    }
}
