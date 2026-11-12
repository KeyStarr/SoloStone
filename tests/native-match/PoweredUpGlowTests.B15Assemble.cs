internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b15-assemble", ProofKind.PoweredUp)]
    private static void B15Assemble()
    {
        // All recipes are unconditional; Shadowcrafter uses the client's ordinary Combo glow.
        Check(!SabberStoneCore.Model.PoweredUp.HasCondition(SabberStoneCore.Model.Cards.FromId("BE_702")),
            "ordinary Combo must not gain a second PoweredUp condition");
    }
}
