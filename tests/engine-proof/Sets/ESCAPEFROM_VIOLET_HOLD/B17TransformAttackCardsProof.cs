using SabberStoneCore.Model;
internal static class B17WeaponCardsProof
{
    [GroupProof("b17-transform-attack-violet-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("JAIL_458", B17TransformAttackCardsProof.Unpinned_Card);
        StandardSetsProof.Case("JAIL_875", B17TransformAttackCardsProof.Unpinned_Card);
    }
}
