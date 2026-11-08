using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FlightManeuvers
    {
        internal const int DbfId = 123139;
        internal static void Configure(Card card)
        {
            ConfigureFamily(card);
            card.Power = new Power { PowerTask = ComplexTask.Create(Summons(), new AddEnchantmentTask("CATA_479e", EntityType.MINIONS)) };
        }
        internal static void ConfigureFirst(Card card)
        {
            ConfigureFamily(card);
            card.Power = new Power { PowerTask = Summons() };
        }
        internal static void ConfigureSecond(Card card)
        {
            ConfigureFamily(card);
            card.Power = new Power { PowerTask = new AddEnchantmentTask("CATA_479e", EntityType.MINIONS) };
        }
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(1), new Effect(GameTag.DIVINE_SHIELD, EffectOperator.SET, 1)) };
        }
        private static ISimpleTask Summons() => EffectContext.Task(ctx => {
            ctx.Summon("CATA_479t3"); ctx.Summon("CATA_479t3");
            return TaskState.COMPLETE;
        });
        private static void ConfigureFamily(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Shatter = new ShatterDefinition("CATA_479", "CATA_479t", "CATA_479t2");
        }
    }
}
