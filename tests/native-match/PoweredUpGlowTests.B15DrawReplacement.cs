using System;
using SabberStoneCore.Model;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b15-draw-replacement", ProofKind.PoweredUp)]
    private static void RunB15DrawReplacement()
    {
        var fixture = new Fixture();
        foreach (string id in new[] { "BE_250", "CORE_SCH_717", "JAIL_398", "TIME_032", "TIME_617", "TLC_513" })
        {
            fixture.ClearHand();
            var card = fixture.Draw(id);
            fixture.Flush();
            Check(!PoweredUp.HasCondition(card.Card), "unconditional/passive card must not acquire conditional glow: " + id);
            fixture.Expected(card, 0, id + " has no conditional bonus");
        }
        Console.WriteLine("PASS draw replacement unconditional/passive cards have no PoweredUp glow");
    }
}
