internal static class B17TargetingConditionsCOREProof
{
    [GroupProof("b17-targeting-conditions-core-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("CS3_022", B17TargetingConditionsProof.Freebooter);
    }
}
