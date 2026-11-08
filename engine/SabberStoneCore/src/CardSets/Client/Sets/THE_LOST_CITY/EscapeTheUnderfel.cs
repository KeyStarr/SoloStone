using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EscapeTheUnderfel
    {
        [StandardCard("TLC_446", 117827)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            QuestDefinition.Register(new QuestDefinition(117827, 6, QuestRewardKind.CardToHand, "TLC_446t"));
            QuestRules.RegisterTemporaryPlay(117827);
            card.Power = new Power();
        }

        [StandardCard("TLC_446t", 118227)]
        internal static void Reward(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new SummonTask("TLC_446t1", SummonSide.DEFAULT) };
        }

        [StandardCard("TLC_446t1", 120968)]
        internal static void Rift(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
    }
}
