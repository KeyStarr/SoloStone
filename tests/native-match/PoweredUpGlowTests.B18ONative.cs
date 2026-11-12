using System;
using SabberStoneCore.Model;
internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b18-o-native", ProofKind.PoweredUp)]
    private static void RunB18ONative()
    {
        foreach(string id in new[]{"CORE_WON_145","CS3_035","EDR_780","EDR_780e1","JAIL_703"})
            Check(Cards.FromId(id).Implemented && !PoweredUp.HasCondition(Cards.FromId(id)),"no invented glow: "+id);
        Console.WriteLine("PASS native tail unconditional glow inventory");
    }
}
