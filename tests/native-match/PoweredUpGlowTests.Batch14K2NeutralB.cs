using SabberStoneCore.Model;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b14-k2-neutral-b", ProofKind.PoweredUp)]
    private static void RunBatch14K2NeutralB()
    {
        var fixture = new Fixture();
        foreach (string id in new[] { "TIME_045", "TLC_101", "TLC_252", "TLC_829t" })
        {
            var card = fixture.Draw(id); fixture.Flush();
            fixture.Expected(card, 0, "unconditional K2 hand card " + id);
            Check(!PoweredUp.HasCondition(card.Card), "K2 introduces no conditional hand glow " + id);
        }
    }
}
