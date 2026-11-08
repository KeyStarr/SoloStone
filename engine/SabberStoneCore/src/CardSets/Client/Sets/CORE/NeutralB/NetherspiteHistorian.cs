using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class NetherspiteHistorian
    {
        internal const int DbfId = 120172;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (LegacyPoweredUpRules.HoldingDragon((IPlayable)s))
                    new EffectContext(g, c.Id, s.Id, stack: stack).Discover(CoreCostPools.Dragon(), recipeDbfId: DbfId);
            }) };
        }
    }
}
