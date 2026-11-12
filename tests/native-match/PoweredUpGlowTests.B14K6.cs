using System;
using SabberStoneCore.Model;
internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b14-k6-rogue-warlock", ProofKind.PoweredUp)]
    private static void RunB14K6()
    {
        var fixture = new Fixture();
        foreach (string id in new[] { "BE_093","DINO_402","JAIL_515","TLC_451","TLC_521","CATA_202","CATA_786","JAIL_474" })
        {
            fixture.ClearHand(); var card=fixture.Draw(id); fixture.Flush();
            fixture.Expected(card,0,"unconditional K6 hand card "+id);
            Check(!PoweredUp.HasCondition(card.Card),"no invented glow condition for "+id);
        }
    }
}
