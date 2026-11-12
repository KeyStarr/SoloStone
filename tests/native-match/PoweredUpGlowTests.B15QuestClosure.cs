using SabberStoneCore.Model;
internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-b15-quest-closure", ProofKind.PoweredUp)]
    private static void RunB15QuestClosure()
    {
        var f = new Fixture();
        foreach (string id in new[] { "TLC_446", "TLC_446t", "TLC_446t1", "TLC_229", "TLC_229t14", "TLC_602", "TLC_602t",
            "TLC_817", "TLC_817t", "TLC_817t2", "TLC_817t3", "TLC_817t4", "TLC_817t5" })
        {
            f.ClearHand(); var card = f.Draw(id); f.Flush();
            Check(!PoweredUp.HasCondition(card.Card), id + " has no conditional play effect");
            f.Expected(card, 0, id + " unconditional quest/reward");
        }
    }
}
