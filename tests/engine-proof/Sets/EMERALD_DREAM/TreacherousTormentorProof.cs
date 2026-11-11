using SabberStoneCore.Enums;
using SabberStoneCore.Model;

internal static class TreacherousTormentorProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_102", (g, p, id) =>
        GiftedDiscoverProof.Exercise(g.Seed, id, GiftedDiscoverProof.AllClasses,
            OfflinePoolFilter.Discover(type: CardType.MINION, rarity: Rarity.LEGENDARY)));
}
