using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WanderingSeeker
    {
        internal const int DbfId = 130113;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_PLAY_CARD) {
                TriggerSource = TriggerSource.FRIENDLY,
                Condition = SelfCondition.IsComboCard,
                SingleTask = ComplexTask.Create(new RandomTask(1, EntityType.ENEMIES), new DamageTask(3, EntityType.STACK))
            } };
        }
    }
}
