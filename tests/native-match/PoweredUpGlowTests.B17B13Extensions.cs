using System;
using SabberStoneCore.Model;
internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b17-b13-extensions", ProofKind.PoweredUp)]
    private static void RunB17B13Extensions()
    {
        foreach (string id in new[] { "CATA_497", "CATA_726", "MEND_304", "TIME_064", "TLC_241", "TLC_250" })
            Check(Cards.FromId(id).Implemented && !PoweredUp.HasCondition(Cards.FromId(id)), "registered without invented glow: " + id);
        Console.WriteLine("PASS b17 extensions unconditional glow inventory");
    }
}
