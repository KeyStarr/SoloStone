using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RestoreTheWild
    {
        internal const int DbfId = 117661;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            QuestDefinition.Register(new QuestDefinition(DbfId, 3, QuestRewardKind.CardToHand,
                "TLC_239t", countsFullBoardTurns: true));
            card.Power = new Power();
        }
    }
}
