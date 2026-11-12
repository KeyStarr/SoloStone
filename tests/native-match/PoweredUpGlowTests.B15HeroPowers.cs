using System;
using SabberStoneCore.Model;
internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b15-heropowers", ProofKind.PoweredUp)]
    private static void RunB15HeroPowers()
    {
        foreach (string id in new[] { "CATA_615", "CORE_CFM_670", "EDR_264", "EDR_451", "JAIL_446", "TLC_436", "TLC_632" })
            Check(!PoweredUp.HasCondition(Cards.FromId(id)), "no artificial conditional glow: " + id);
        Console.WriteLine("PASS b15 PoweredUp: seven unconditional recipes");
    }
}
