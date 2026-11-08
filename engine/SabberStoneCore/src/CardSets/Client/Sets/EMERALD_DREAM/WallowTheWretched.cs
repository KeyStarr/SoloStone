using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WallowTheWretched
    {
        internal const int DbfId = 115044;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.DARK_GIFT_GIVEN) {
                TriggerActivation = TriggerActivation.HAND_OR_DECK,
                TriggerSource = TriggerSource.FRIENDLY,
                Condition = new SelfCondition(recipient => recipient is Minion),
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    // D8 / dg17b-wallow-event-semantics.yaml: self-grants also mirror once.
                    DarkGifts.Mirror((IPlayable)s, g.CurrentEventData.EventSource.Card.Id,
                            (IPlayable)g.CurrentEventData.EventSource);
                })
            } };
        }
    }
}
