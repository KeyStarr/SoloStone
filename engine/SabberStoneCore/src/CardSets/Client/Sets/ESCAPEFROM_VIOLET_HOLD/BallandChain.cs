using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Conditions;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BallandChain
    {
        internal const int DbfId = 126597;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = EffectContext.Task(ctx => ctx.RunLegacy(ComplexTask.Create(new IncludeTask(EntityType.MINIONS), new FilterStackTask(SelfCondition.IsDamaged), new AddEnchantmentTask("JAIL_376e", EntityType.STACK)))) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(1), Effects.Health_N(2)) };
        }
    }
}
