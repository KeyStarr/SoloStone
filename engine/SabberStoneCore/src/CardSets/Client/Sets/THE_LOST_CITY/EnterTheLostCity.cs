using System.Linq;
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
    internal static class EnterTheLostCity
    {
        [StandardCard("TLC_602", 117716)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            QuestDefinition.Register(new QuestDefinition(117716, 10, QuestRewardKind.CardToHand, "TLC_602t"));
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                TriggerSource = TriggerSource.FRIENDLY,
                SingleTask = new CustomTask((g, c, s, t, stack) => QuestRules.Advance((Spell)s))
            } };
        }
    }
}
