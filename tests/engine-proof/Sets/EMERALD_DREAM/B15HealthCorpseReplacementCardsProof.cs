using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B15HealthCorpseReplacementCardsProof
{
    [GroupProof("b15-health-corpse-replacement-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("CATA_155", (original, _, id) => { var g = DeathKnightC1Proof.NewGame(original.Seed); ArisenOnyxiaProof.Unpinned_HealthCostsAndWings(g, g.Player1, id); });
        StandardSetsProof.Case("CORE_EDR_003", (original, _, id) => { var g = DeathKnightC1Proof.NewGame(original.Seed); FalricProof.Unpinned_DrawAndGain(g, g.Player1, id); });
        StandardSetsProof.Case("EDR_489", (original, _, id) => { var g = DeathKnightC1Proof.NewGame(original.Seed); AgamagganProof.Unpinned_OpponentPayment(g, g.Player1, id); });
        StandardSetsProof.Case("EDR_813", (original, _, id) => { var g = DeathKnightC1Proof.NewGame(original.Seed); MorbidSwarmProof.Unpinned_IndependentBranches(g, g.Player1, id); });
    }
}
