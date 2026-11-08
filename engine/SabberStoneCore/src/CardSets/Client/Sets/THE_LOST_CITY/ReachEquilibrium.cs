using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ReachEquilibrium
    {
        [StandardCard("TLC_817", 117544)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            QuestDefinition.Register(new QuestDefinition(117544, 4, QuestRewardKind.CardToHand, "TLC_817t5"));
            QuestRules.RegisterGroup(117544, "TLC_817t", "TLC_817t2");
            QuestRules.RegisterHandPair("TLC_817t3", "TLC_817t4", "TLC_817t5");
            card.Power = new Power();
        }

        [StandardCard("TLC_817t", 117545)]
        internal static void Holy(Card card) => Component(card, 5, "TLC_817t3");
        [StandardCard("TLC_817t2", 119726)]
        internal static void Shadow(Card card) => Component(card, 6, "TLC_817t4");
        private static void Component(Card card, int school, string reward)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            QuestDefinition.Register(new QuestDefinition(card.AssetId, 4, QuestRewardKind.CardToHand, reward));
            QuestRules.RegisterComponent(card.AssetId, 117544);
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_CAST) {
                TriggerSource = TriggerSource.FRIENDLY,
                Condition = new SelfCondition(p => p[GameTag.SPELL_SCHOOL] == school),
                SingleTask = new CustomTask((g, c, s, t, stack) => QuestRules.Advance((Spell)s))
            } };
        }
    }
}
