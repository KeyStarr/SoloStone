internal static class B17TargetingConditionsCATACLYSMProof
{
    [GroupProof("b17-targeting-conditions-cataclysm-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("CATA_161", B17TargetingConditionsProof.Nightmare);
        StandardSetsProof.Case("CATA_472", B17TargetingConditionsProof.Unpinned_Maul);
        StandardSetsProof.Case("CATA_898", B17TargetingConditionsProof.Lancer);
        StandardSetsProof.Case("MEND_040", B17TargetingConditionsProof.Worm);
    }
}
