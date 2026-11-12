using System;
using SabberStoneCore.Model;
internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b17-transform-attack", ProofKind.PoweredUp)]
    private static void RunB17TransformAttack()
    {
        var f = new Fixture();
        foreach (var id in new[] { "DINO_414", "EDR_529", "JAIL_458", "JAIL_875" })
        {
            f.ClearHand(); var card = f.Draw(id); f.Flush();
            Check(!PoweredUp.HasCondition(card.Card), "no conditional bonus: " + id);
            f.Expected(card, 0, id);
        }
        Console.WriteLine("PASS b17 transform/attack hand glow");
    }
}
