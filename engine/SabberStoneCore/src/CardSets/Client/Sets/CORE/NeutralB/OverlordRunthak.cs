using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class OverlordRunthak
    {
        internal const int DbfId = 66858;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // ATTACK is dispatched before combat damage in Attack.OnAttackTrigger.
            card.Power = new Power { Trigger = new Trigger(TriggerType.ATTACK) {
                TriggerSource = TriggerSource.SELF,
                SingleTask = ComplexTask.Create(new IncludeTask(EntityType.HAND),
                    new FilterStackTask(SelfCondition.IsMinion), new AddEnchantmentTask("CS3_025e", EntityType.STACK))
            } };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(1), Effects.Health_N(1)) };
        }
    }
}
