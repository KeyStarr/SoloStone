using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FragmentofNothing
    {
        internal const int DbfId = 121189;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_CAST) {
                TriggerSource = TriggerSource.FRIENDLY,
                OwnerCondition = new SelfCondition(owner => owner.Game.CurrentEventData?.EventSource is Spell spell && spell.Game.IdEntityDic.TryGetValue(spell.CardTarget, out var target) && target is Minion),
                SingleTask = new DrawTask()
            } };
        }
    }
}
