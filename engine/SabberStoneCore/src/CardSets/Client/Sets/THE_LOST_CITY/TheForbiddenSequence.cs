using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TheForbiddenSequence
    {
        [StandardCard("TLC_460", 118183)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            QuestDefinition.Register(new QuestDefinition(118183, 8, QuestRewardKind.CardToHand, "TLC_460t"));
            card.Power = new Power();
        }
        [StandardCard("TLC_460t", 118191)]
        internal static void Stone(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
    }
}
