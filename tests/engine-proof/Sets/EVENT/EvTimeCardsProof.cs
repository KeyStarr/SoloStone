internal static class EvTimeCardsProof
{
    [GroupProof("ev-time-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("TIME_EVENT_998", EvTimeProof.Unpinned_RuniClockState);
        StandardSetsProof.Case("CATA_EVENT_001", EvTimeProof.Unpinned_PhoenixClockCopy);
        StandardSetsProof.Case("TIME_EVENT_999", EvTimeProof.Unpinned_SandsSwitch);
    }
}
