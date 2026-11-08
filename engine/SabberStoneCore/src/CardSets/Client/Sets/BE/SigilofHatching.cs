using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SigilofHatching
    {
        internal const int DbfId = 130680;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                new Trigger(TriggerType.TURN_START) {
                    RemoveAfterTriggered = true,
                    SingleTask = new SummonTask("BE_873t", 3)
                }.Activate((IPlayable)s, asAncillary: ((IPlayable)s).ActivatedTrigger != null);
            }) };
        }
    }
}
