using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ScrappyScavenger
    {
        internal const int DbfId = 118192;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                stack ??= new TaskStack();
                stack.Number = c.RemainingMana;
                new EffectContext(g, c.Id, s.Id, stack: stack).Discover(TheLostCityPools.ScavengerCard(),
                    costBinding: PoolCostBinding.StackNumber, recipeDbfId: DbfId);
            }) };
        }
    }
}
