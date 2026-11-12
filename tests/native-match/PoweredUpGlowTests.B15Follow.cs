using System;
using SabberStoneCore.Model;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b15-follow", ProofKind.PoweredUp)]
    private static void RunB15Follow()
    {
        var f = new Fixture();
        foreach (string id in new[] { "CAP_002", "CAP_101", "CAP_402", "CAP_802" }) {
            var card = f.Draw(id); f.Flush();
            Check(Cards.FromId(id)?.Implemented == true, "Follow registered: " + id);
            f.Expected(card, 0, "Follow has no pre-play bonus condition: " + id);
        }
        Console.WriteLine("PASS Follow PoweredUp inventory: four unconditional spells");
    }
}
