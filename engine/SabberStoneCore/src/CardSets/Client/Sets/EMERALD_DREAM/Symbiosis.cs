using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Symbiosis
    {
        internal const int DbfId = 115876;
        internal static readonly OfflinePoolFilter Filter = new OfflinePoolFilter(
            classMode: PoolClassMode.OtherClass, keyword: GameTag.CHOOSE_ONE);
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                ctx.Discover(Filter, recipeDbfId: DbfId);
                return TaskState.COMPLETE;
            }) };
        }
    }
}
