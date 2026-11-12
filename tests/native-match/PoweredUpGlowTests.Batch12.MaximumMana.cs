using System;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b12-maximum-mana", ProofKind.PoweredUp)]
    private static void RunBatch12MaximumMana()
    {
        foreach (bool met in new[] { false, true })
        {
            var f = new Fixture();
            var card = f.Draw("JAIL_507");
            f.Mana(met ? 10 : 9);
            f.Player.UsedMana = met ? 6 : 0;
            f.Player.TemporaryMana = met ? 0 : 1;
            f.Flush();
            f.Expected(card, met ? 1 : 0, "Chef permanent maximum Mana, including spent and excluding temporary");
            f.Process(PlayCardTask.Minion(f.Player, card));
            f.Expected(card, 0, "Chef after play");
            var summoned = f.Player.BoardZone.Single(m => m.Id != card.Id);
            Check(summoned.HasTaunt && summoned.Card.Cost == (met ? 6 : 2), "Chef summons threshold's exact-cost Taunt");
        }
        OutcomeCases.Add("JAIL_507");
    }
}
