using System;
using SabberStoneCore.Model;
internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b16-c16", ProofKind.PoweredUp)]
    private static void RunB16C16()
    {
        var fixture = new Fixture();
        foreach (string id in new[] { "JAIL_509", "JAIL_800", "TLC_830", "TLC_830t" })
        {
            fixture.ClearHand(); var card = fixture.Draw(id); fixture.Flush();
            Check(!PoweredUp.HasCondition(card.Card), "unconditional/start-of-game card: " + id);
            fixture.Expected(card, 0, id + " has no hand bonus");
        }
    }
}
