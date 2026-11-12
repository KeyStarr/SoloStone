using SabberStoneCore.Enums;
using SabberStoneCore.Model;

internal static class CreatureofMadnessProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_105", (g, p, id) =>
        GiftedDiscoverProof.Exercise(g.Seed, id, GiftedDiscoverProof.AllClasses,
            OfflinePoolFilter.Discover(type: CardType.MINION, minCost: 3, maxCost: 3)));
}
