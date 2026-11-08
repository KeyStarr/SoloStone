using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WildwoodCircle
    {
        internal const int DbfId = 122971;
        internal static void Configure(Card card)
        {
            ConfigureFamily(card);
            card.Power = new Power { PowerTask = ComplexTask.Create(Summons(), new AddEnchantmentTask("CATA_134e", EntityType.MINIONS)) };
        }
        internal static void ConfigureFirst(Card card)
        {
            ConfigureFamily(card);
            card.Power = new Power { PowerTask = Summons() };
        }
        internal static void ConfigureSecond(Card card)
        {
            ConfigureFamily(card);
            card.Power = new Power { PowerTask = new AddEnchantmentTask("CATA_134e", EntityType.MINIONS) };
        }
        internal static void ConfigureDeathrattle(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                new EffectContext(g, c.Id, s.Id).Summon("CATA_134t3", EffectContext.DeathrattlePosition(c, s));
            }) };
        }
        private static ISimpleTask Summons() => EffectContext.Task(ctx => {
            ctx.Summon("CATA_134t3"); ctx.Summon("CATA_134t3");
            return TaskState.COMPLETE;
        });
        private static void ConfigureFamily(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Shatter = new ShatterDefinition("CATA_134", "CATA_134t", "CATA_134t2");
        }
    }
}
