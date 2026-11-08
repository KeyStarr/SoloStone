using System.Linq;
using SabberStoneCore.Actions;
using System.Collections.Generic;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ShadowflameSuffusion
    {
        internal const int DbfId = 115688;
        internal static readonly OfflinePoolFilter Filter = new OfflinePoolFilter(classMode: PoolClassMode.Exact, exactClass: CardClass.WARRIOR, type: CardType.MINION);
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } });
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/FIR_939/v1",
                EffectContext.Task(ctx => { if (ctx.TargetId.HasValue) ctx.Damage(ctx.TargetId.Value, 2, spellDamage: true); return TaskState.COMPLETE; }),
                new DiscoverWithDarkGiftThenContinueTask(Filter, recipeDbfId: DbfId),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.SingleOrDefault();
                    if (chosen == null || !Generic.RemoveFromZone(c, chosen)) return;
                    Generic.AddHandPhase(c, chosen);
                    DarkGifts.ResolveSweetDreams(chosen);
                })) };
        }
    }
}
