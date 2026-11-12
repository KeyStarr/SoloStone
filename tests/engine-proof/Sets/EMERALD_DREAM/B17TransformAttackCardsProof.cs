using SabberStoneCore.Model;
internal static class B17PodlingCardsProof
{
    [GroupProof("b17-transform-attack-emerald-cards", ProofKind.Cards)]
    internal static void Run() => StandardSetsProof.Case("EDR_529", B17TransformAttackCardsProof.Unpinned_Card);
}
