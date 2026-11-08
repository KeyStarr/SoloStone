using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BarricadeBasher
    {
        internal const int DbfId = 118403;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.ARMOR) {
                TriggerSource = TriggerSource.HERO,
                Condition = new SelfCondition(p => p.Game.CurrentEventData.EventNumber > 0),
                SingleTask = ComplexTask.Create(
                    new AddEnchantmentTask("DINO_400e", EntityType.SOURCE),
                    new RandomTask(1, EntityType.OP_MINIONS),
                    new AttackTask(EntityType.SOURCE, EntityType.STACK))
            } };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(2)) };
        }
    }
}
