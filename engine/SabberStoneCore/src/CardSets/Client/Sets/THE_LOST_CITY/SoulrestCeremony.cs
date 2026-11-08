using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SoulrestCeremony
    {
        internal const int DbfId = 118559;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new AddEnchantmentTask("DINO_417e", EntityType.MINIONS) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power {
                Enchant = new Enchant(Effects.Attack_N(1), Effects.Rush),
                Trigger = new MultiTrigger(
                    new Trigger(TriggerType.TURN_END) {
                        RemoveAfterTriggered = true,
                        SingleTask = new DestroyTask(EntityType.TARGET)
                    },
                    // Control changes also emit ZONE; only leaving PLAY cancels the mark.
                    new Trigger(TriggerType.ZONE) {
                        TriggerSource = TriggerSource.ENCHANTMENT_TARGET,
                        Condition = new SelfCondition(p => p.Zone?.Type != Zone.PLAY),
                        FastExecution = true,
                        SingleTask = new CustomTask((g, c, s, t, stack) => ((Enchantment)s).Remove())
                    })
            };
        }
    }
}
