#if CLIENT_TRANSPORT
using CoreLegacy;
using SabberStoneCore.Model;

/// <summary>
/// The engine-proof-client suite (docs/TESTING.md): what the engine-proof suite cannot run without a client. It is the same
/// source built with the client's libraries (CLIENT_TRANSPORT) and run with the card export built from that client.
///   1. The proofs that compare the engine with the client's data: the card definitions file, hero powers, the independent
///      pool derivation and the printed class domains (ClientExport.Oracle is true here and nowhere else).
///   2. The native transport halves of the proofs whose engine halves also run in the engine-proof suite: the same proof is
///      run again here with its tail compiled in. The tails check that the client receives the same packets whatever hidden
///      cards the opponent holds, and the pet and likes projections.
///   3. That the NativeMatch.TransportCardId the plugin ships and the mirror the suites without a client link agree.
/// </summary>
internal static class ClientTier
{
    internal static void Run()
    {
        ClientExport.ClientTier = true;
        Core2026Proof.Check(ClientExport.Present, "engine-proof-client needs the card export built from your client: " + ClientExport.CardsPath);
        TransportCardIdAgrees();
        Console.WriteLine("PASS NativeMatch.TransportCardId equals its mirror for every card id");

        CardFactsProof.Run();
        ClassHeroesProof.Run();
        AssembleProof.Catalog();
        OfflinePoolEligibilityProof.Run();
        OfflinePoolAdmissionProof.Run();
        PastVarietySpellsProof.Run();

        HistoryHandDisplayProof.Register();
        ImbueProof.Run();
        AgentCoinChoiceProof.Run();
        LegendaryEliteProof.Run();
        HistoryHandDisplayProof.Run();
        PetTransportProof.Run();   // a Facility group proof: the engine-proof suite finds it by reflection, so it needs the client build
        // Case() records a failure instead of throwing, and StandardSetsProof.Run clears the record when it starts: nothing that
        // calls Case may run directly before it. Leyline and Batch14K7 go through the selected list below.
        StandardSetsProof.ThrowFailures();
        StandardSetsProof.Run(selectedProofs: new Action[] {
            // Oracle comparisons inside card proofs.
            LeylineProofs.Run, Batch14K7Proof.Run,
            TimelordNozdormuProof.Run, JackpotProof.Run, AmberWardenProof.Run, BloodDrawProof.Run, WhisperingStoneProof.Run, B14K3BEProof.Run,
            CoreBallOfSpidersProof.Run, CoreJeweledMacawProof.Run, CoreShakuTheCollectorProof.Run, CoreSwashburglarProof.Run,
            CoreWebspinnerProof.Run, BlazingInvocationProof.Run, WitchsApprenticeProof.Run, BrightwingProof.Run, GorillabotA3Proof.Run,
            NetherspiteHistorianProof.Run, IvoryKnightProof.Run, SilvermoonPortalProof.Run,
            // Transport tails inside card proofs.
            KnockbackProof.Run, StarsurgeProof.Run, LotusTroublemakerProof.Run,
            LightoftheNewMoonProof.Run, RitualoftheNewMoonProof.Run, ReforestationProof.Run, StormfuryProof.Run,
            SplinteredRealityProof.Run, ClockworkRagerProof.Run });
        Console.WriteLine("engine-proof-client: PASS");
    }

    private static void TransportCardIdAgrees()
    {
        var ids = Cards.All.Select(card => card.Id).Concat(new[] { null, "", "CFM_712_t", "CFM_712_t0", "CFM_712_tab", "CFM_712_t31", "CFM_712_t100", "UNKNOWN" })
            .Concat(Enumerable.Range(0, 32).Select(size => "CFM_712_t" + size.ToString("00")))
            .Concat(new[] { "CS2_034", "CS2_102", "CS1h_001", "CS2_101", "CS2_017", "DS1h_292", "CS2_083b", "CS2_056", "CS2_049" })
            .Concat(new[] { "WARRIOR", "SHAMAN", "ROGUE", "PALADIN", "HUNTER", "DRUID", "WARLOCK", "MAGE", "PRIEST" }.Select(cls => "AT_132_" + cls))
            .Distinct();
        int count = 0;
        foreach (string id in ids)
        {
            Core2026Proof.Check(NativeMatch.TransportCardId(id) == TransportCardIdMirror.Of(id), "TransportCardId mirror differs for " + (id ?? "null"));
            count++;
        }
        Core2026Proof.Check(count > 1000, "mirror compared over the whole card set");
    }
}
#endif
