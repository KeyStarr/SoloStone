using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Tortolla
    {
        internal const int DbfId = 114353;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                Trigger = new Trigger(TriggerType.TAKE_DAMAGE) {
                    TriggerSource = TriggerSource.SELF,
                    Condition = new SelfCondition(p => p.Game.CurrentEventData.EventNumber > 0),
                    SingleTask = ComplexTask.Create(new ArmorTask(1), new AddEnchantmentTask("EDR_471e", EntityType.SOURCE))
                }
            };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(1)) };
        }
    }
}
