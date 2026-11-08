using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HighlordFordragon
    {
        internal const int DbfId = 129847;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.LOSE_DIVINE_SHIELD) {
                TriggerSource = TriggerSource.MINIONS,
                // Silence queues shield loss before removing this trigger.
                SingleTask = ComplexTask.Conditional(new SelfCondition(p => p is Minion m && !m.IsSilenced),
                    ComplexTask.Create(new IncludeTask(EntityType.HAND),
                    new FilterStackTask(new SelfCondition(p => p is Minion)), new RandomTask(1, EntityType.STACK),
                    new AddEnchantmentTask("SW_047e", EntityType.STACK)))
            } };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(5)) };
        }
    }
}
