using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TunnelTerror
    {
        internal const int DbfId = 118282;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id);
                for (int i = 0; i < 2; i++)
                {
                    var picked = ctx.PickRandomCard(TheLostCityPools.Terror(), recipeDbfId: DbfId);
                    if (picked == null) continue;
                    var minion = Entity.FromCard(c, picked, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                    TemporaryRules.Make(minion);
                    Generic.AddHandPhase(c, minion);
                }
            }) };
        }
    }
}
