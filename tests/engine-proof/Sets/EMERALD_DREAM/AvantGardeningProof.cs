using SabberStoneCore.Enums;
using SabberStoneCore.Model;

internal static class AvantGardeningProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_488", (g, p, id) => {
        DecisionChoiceReplayProof.Spell(g.Seed, id, gifted: true);
        GiftedDiscoverProof.Exercise(g.Seed, id, new[] { CardClass.WARLOCK },
            OfflinePoolFilter.Discover(type: CardType.MINION, keyword: GameTag.DEATHRATTLE));
    });
}
