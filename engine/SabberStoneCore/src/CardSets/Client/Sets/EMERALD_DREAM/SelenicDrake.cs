using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SelenicDrake
    {
        internal const int DbfId = 114308;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = EffectContext.Task(ctx => {
                    var chosen = ctx.PickRandomCard(EmeraldDreamPools.Dragon(), recipeDbfId: DbfId);
                    if (chosen != null) ctx.AddToHand(chosen.Id);
                    return TaskState.COMPLETE;
                })
            } };
        }
    }
}
