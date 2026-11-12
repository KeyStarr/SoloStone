internal static class B16StatGainCardsProof
{
    [GroupProof("b16-stat-gain-event-cards", ProofKind.Cards)]
    internal static void Run()
    {
        B16StatGainNeptulonProof.Run();
        B16StatGainWickerfangProof.Run();
        B16StatGainChampionProof.Run();
    }
}
