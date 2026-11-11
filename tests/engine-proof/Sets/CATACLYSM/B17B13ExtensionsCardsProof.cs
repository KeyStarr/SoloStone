internal static class B17B13ExtensionsCardsProof
{
    [GroupProof("b17-b13-extensions-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("MEND_304", (g,p,id) => B17B13ExtensionsProof.Unpinned_Talya());
        StandardSetsProof.Case("CATA_497", (g,p,id) => B17B13ExtensionsProof.Unpinned_Ultraxion());
        StandardSetsProof.Case("CATA_726", (g,p,id) => B17B13ExtensionsProof.Unpinned_ChoGall());
        B17B13LostCityProof.Run();
        B17B13TimeTravelProof.Run();
    }
}
