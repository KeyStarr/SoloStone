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
    internal static class SI7Slayer
    {
        internal const int DbfId = 127011;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Attack.OnAttackTrigger runs before AttackPhase clears Stealth.
            card.Power = new Power { Trigger = new Trigger(TriggerType.ATTACK) {
                TriggerSource = TriggerSource.MINIONS,
                Condition = new SelfCondition(p => ((Minion)p).HasStealth),
                SingleTask = new AddEnchantmentTask("CAP_000e", EntityType.TARGET)
            } };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(2), Effects.Health_N(2)) };
        }
    }
}
