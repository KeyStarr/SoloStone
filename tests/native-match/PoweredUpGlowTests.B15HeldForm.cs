using SabberStoneCore.Model;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b15-held-form", ProofKind.PoweredUp)]
    private static void RunB15HeldForm()
    {
        var f = new Fixture();
        foreach (string id in new[] { "BE_259", "CATA_614", "CORE_RLK_567", "DINO_407", "TIME_876" })
        {
            var card = f.Draw(id); f.Flush();
            Check(!PoweredUp.IsMet(card) && f.Glow(card) == 0, id + " has no conditional play bonus");
        }
    }
}
