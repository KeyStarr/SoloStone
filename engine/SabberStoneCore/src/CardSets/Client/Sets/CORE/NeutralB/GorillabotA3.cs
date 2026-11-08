using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GorillabotA3
    {
        internal const int DbfId = 86548;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (LegacyPoweredUpRules.AnotherMech((IPlayable)s))
                    new EffectContext(g, c.Id, s.Id, stack: stack).Discover(CoreCostPools.Mech(), recipeDbfId: DbfId);
            }) };
        }
    }
}
