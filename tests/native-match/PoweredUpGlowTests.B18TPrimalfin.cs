using System;
using SabberStoneCore.Model;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b18-t-primalfin", ProofKind.PoweredUp)]
    private static void RunB18TPrimalfin()
    {
        var fixture = new Fixture();
        foreach (string id in new[] { "TLC_251", "TLC_251e" })
        {
            Check(!PoweredUp.HasCondition(Cards.FromId(id)), "Primalfin references Kindred without a conditional bonus: " + id);
            if (id == "TLC_251") { var card = fixture.Draw(id); fixture.Flush(); fixture.Expected(card, 0, id); }
        }
        Console.WriteLine("PASS Primalfin has no conditional hand glow");
    }
}
