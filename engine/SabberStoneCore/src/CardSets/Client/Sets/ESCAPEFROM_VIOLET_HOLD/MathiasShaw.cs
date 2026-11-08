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
    internal static class MathiasShaw
    {
        internal const int DbfId = 127056;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.ATTACK) {
                TriggerSource = TriggerSource.MINIONS,
                Condition = new SelfCondition(p => ((Minion)p).HasStealth),
                SingleTask = ComplexTask.Create(new RandomTask(1, EntityType.HAND),
                    new AddEnchantmentTask("CAP_005e4", EntityType.STACK))
            } };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.ReduceCost(3)) };
        }
    }
}
