using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WindsweptPageturner
    {
        internal const int DbfId = 117531;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_SUMMON) {
                TriggerSource = TriggerSource.MINIONS_EXCEPT_SELF,
                Condition = new SelfCondition(p => p.Card.IsRace(Race.ELEMENTAL)),
                SingleTask = ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 3)
            } };
        }
    }
}
