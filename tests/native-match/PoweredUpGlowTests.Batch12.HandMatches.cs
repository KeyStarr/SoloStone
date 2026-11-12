using SabberStoneCore.Model.Zones;
using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b12-hand-matches", ProofKind.PoweredUp)]
    private static void RunBatch12HandMatches()
    {
        IPlayable match = null;
        Scenario("EDR_524", (f, card, met) => {
            foreach (var held in f.Game.Player2.HandZone.ToArray())
                f.Game.Player2.SetasideZone.Add(f.Game.Player2.HandZone.Remove(held));
            // The source must not match itself while it is still in hand.
            SabberStoneCore.Actions.Generic.DrawCard(f.Game.Player2, Cards.FromId("EDR_524"));
            match = SabberStoneCore.Actions.Generic.DrawCard(f.Game.Player2, Cards.FromId("CS2_182"));
            if (met) f.Draw("CS2_182");
        }, (f, card) => PlayCardTask.Minion(f.Player, card), (f, card) => {
            bool moved = match.Zone == f.Game.Player2.DeckZone;
            Check(f.Game.Player2.HandZone.Count == (moved ? 1 : 2)
                && f.Game.Player2.HandZone.Any(c => c.Card.Id == "EDR_524")
                && (!moved || f.Player.HandZone.Single().Card.Id == "CS2_182"),
                "Assailant shuffles one shared identity, excludes source and retains own match");
            return moved;
        });

        Scenario("EDR_524", (f, card, met) => {
            foreach (var held in f.Game.Player2.HandZone.ToArray())
                f.Game.Player2.SetasideZone.Add(f.Game.Player2.HandZone.Remove(held));
            f.Draw("CS2_182");
            match = SabberStoneCore.Actions.Generic.DrawCard(f.Game.Player2, Cards.FromId("CS2_182"));
            var deck = f.Game.Player2.DeckZone;
            while (deck.Count < (met ? (DeckZone.DeckMaximumCapcity - 1) : DeckZone.DeckMaximumCapcity))
                deck.Add(SabberStoneCore.Model.Entities.Entity.FromCard(f.Game.Player2, Cards.FromId("CS2_200")));
        }, (f, card) => PlayCardTask.Minion(f.Player, card), (f, card) => {
            bool moved = match.Zone == f.Game.Player2.DeckZone;
            Check(f.Game.Player2.DeckZone.Count == DeckZone.DeckMaximumCapcity && f.Player.HandZone.Single().Card.Id == "CS2_182"
                && f.Game.Player2.HandZone.Count == (moved ? 0 : 1),
                "Assailant needs a free enemy deck slot and preserves the shared card at capacity");
            return moved;
        });

        foreach (bool preparation in new[] { true, false })
        {
            IPlayable held = null;
            Scenario("BE_086", (f, card, met) => {
                card.Cost = 4;
                held = f.Draw("CS2_029");
                held.Cost = met ? 2 : 4;
                if (preparation)
                    f.Process(PlayCardTask.Spell(f.Player, f.Draw("CORE_EX1_145")));
                else
                    SabberStoneCore.Actions.CostRules.AddNextCardRule(f.Player, cardType: CardType.SPELL, setCost: 2);
                Check(card.Cost == 2 && (met || held.Cost == 2), "shared discount creates a pre-play Cost tie");
            }, (f, card) => PlayCardTask.Spell(f.Player, card), (f, card) => {
                int damage = f.Game.Player2.Hero.Damage;
                Check(held.Zone == f.Player.HandZone && held.Cost == (damage == 4 ? 2 : 4)
                    && (damage == 2 || damage == 4),
                    "Blasphemy glow predicts remaining hand Costs after Preparation or one-use rule consumption");
                return damage == 4;
            });
        }
    }
}
