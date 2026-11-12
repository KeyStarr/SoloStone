using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b12-discover", ProofKind.PoweredUp)]
    private static void RunBatch12Discover()
    {
        Scenario("JAIL_734", (f, card, met) => {
            if (met)
                foreach (var held in f.Player.DeckZone.ToArray()) f.Player.SetasideZone.Add(f.Player.DeckZone.Remove(held));
        }, (f, card) => PlayCardTask.Minion(f.Player, card), (f, card) => {
            var minion = (Minion)card;
            bool buffed = minion.AttackDamage == 6 && minion.Health == 6;
            Check(minion.HasTaunt && (buffed ? f.Player.Choice == null : f.Player.Choice != null),
                "Hellraiser glow predicts empty-deck buff; nonempty deck opens real Discover");
            if (!buffed) f.Process(ChooseTask.Pick(f.Player, f.Player.Choice.Choices[0]));
            return buffed;
        });
    }
}
