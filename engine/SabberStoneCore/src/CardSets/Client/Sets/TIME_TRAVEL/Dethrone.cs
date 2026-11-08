using System.Collections.Generic;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Dethrone
    {
        internal const int DbfId = 120797;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.Power = new Power {
                PowerTask = new DestroyTask(EntityType.TARGET),
                ComboTask = new CustomTask((g, c, s, t, stack) => {
                    var ctx = new EffectContext(g, c.Id, s.Id, t?.Id, stack);
                    if (t != null) ctx.Destroy(t.Id);
                    var chosen = ctx.PickRandomCard(TimeTravelPools.EightCostMinion(), recipeDbfId: DbfId);
                    if (chosen != null) ctx.Summon(chosen.Id);
                })
            };
        }
    }
}
