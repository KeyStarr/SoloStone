using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Collections.Generic;
using System.Linq;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RiteOfTwilight
    {
        internal const int DbfId = 123605;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { [PlayReq.REQ_TARGET_FOR_COMBO] = 0 });
            card.Power = new Power {
                PowerTask = EffectContext.Task(ctx => {
                    ctx.Herald();
                    return TaskState.COMPLETE;
                }),
                ComboTask = new CustomTask((g, c, source, target, stack) => {
                    var ctx = new EffectContext(g, c.Id, source.Id, stack: stack);
                    var cast = ctx.History.Casts().LastOrDefault(f => f.EntityId == source.Id);
                    // Player also covers forced deck plays; its hand residence must close during this play.
                    var play = ctx.History.Plays().LastOrDefault(f => f.EntityId == source.Id);
                    var hand = ctx.History.Residences(source.Id, Zone.HAND).LastOrDefault();
                    bool fromHand = play != null && hand.ControllerId == c.Id && hand.ExitSequence.HasValue
                        && hand.ExitSequence.Value > play.PlayId && hand.ExitSequence.Value < play.Sequence;
                    ctx.Herald();
                    if (fromHand && cast?.CasterKind == HistoryCastKind.Player && cast.Outcome == HistoryCastOutcome.Started && target != null)
                        new DamageTask(3, EntityType.TARGET, true).Process(g, c, source, target, stack);
                })
            };
        }
    }
}
