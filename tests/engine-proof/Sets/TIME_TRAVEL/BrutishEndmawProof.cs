using SabberStoneCore.Enums;
using SabberStoneCore.Model;

internal static class BrutishEndmawProof
{
    internal static void Run() => StandardSetsProof.Case("END_013", (g, p, id) =>
        GiftedDiscoverProof.Exercise(g.Seed, id, new[] { CardClass.HUNTER, CardClass.PALADIN },
            OfflinePoolFilter.Discover(type: CardType.MINION, minCost: 1, maxCost: 1)));
}
