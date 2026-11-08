using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SparkOfLife
    {
        internal const int DbfId = 116977;
        internal static OfflinePoolFilter Filter(CardClass hero) => new OfflinePoolFilter(
            classMode: PoolClassMode.Exact, exactClass: hero, type: CardType.SPELL);
        private static ISimpleTask Discover(CardClass hero) => EffectContext.Task(ctx => {
            ctx.Discover(Filter(hero), recipeDbfId: DbfId);
            return TaskState.COMPLETE;
        });
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = ComplexTask.Create(Discover(CardClass.MAGE), Discover(CardClass.DRUID)) };
        }
        internal static void ConfigureMage(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = Discover(CardClass.MAGE) };
        }
        internal static void ConfigureDruid(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = Discover(CardClass.DRUID) };
        }
    }
}
