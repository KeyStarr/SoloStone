using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FastForward
    {
        internal const int DbfId = 121142;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/TIME_770/v1",
                new DrawAndCaptureTask(2),
                new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.CapturedEntities)),
                EffectContext.Task(ctx => ctx.LastChosenEntityId == 0 ? TaskState.COMPLETE :
                    ctx.RunLegacy(new ApplyEffectTask(EntityType.TARGET, Effects.ReduceCost(2)), ctx.LastChosenEntityId))) };
        }
    }
}
