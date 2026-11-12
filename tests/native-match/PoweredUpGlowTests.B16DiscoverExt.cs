using System;
using SabberStoneCore.Model;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b16-discover-ext", ProofKind.PoweredUp)]
    private static void RunB16DiscoverExt()
    {
        var f = new Fixture();
        foreach (string id in new[] { "JAIL_319", "TIME_861", "TLC_464", "TLC_483", "TLC_515" }) {
            var card = f.Draw(id); f.Flush();
            Check(Cards.FromId(id).Implemented, "Discover extension registered: " + id);
            f.Expected(card, 0, "no pre-play conditional bonus: " + id);
        }
        Console.WriteLine("PASS Discover extensions PoweredUp inventory");
    }
}
