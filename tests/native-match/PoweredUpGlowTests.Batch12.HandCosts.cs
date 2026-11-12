using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b12-hand-costs", ProofKind.PoweredUp)]
    private static void RunBatch12HandCosts()
    {
        foreach (int cost in new[] { 1, 4, 6 })
            Scenario("BE_086", (f, card, met) => {
                card.Cost = cost;
                f.Draw("CS2_182").Cost = met ? cost : cost + 1;
                f.Summon("CS2_200", f.Player);
                f.Summon("CS2_200", f.Game.Player2);
            }, (f, card) => PlayCardTask.Spell(f.Player, card), (f, card) => {
                int damage = f.Game.Player2.Hero.Damage;
                Check((damage == 2 || damage == 4) && f.Game.Player2.BoardZone.Single().Damage == damage
                    && f.Player.BoardZone.Single().Damage == 0 && f.Player.Hero.Damage == 0,
                    "Blasphemy repeats enemy-only damage at highest effective Cost, including ties");
                return damage == 4;
            });
    }
}
