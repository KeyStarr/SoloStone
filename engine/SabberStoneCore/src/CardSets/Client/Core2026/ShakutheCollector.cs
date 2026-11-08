using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Core2026
{
    internal static class ShakutheCollector
    {
        internal const int DbfId = 120463;
        internal static void Configure(Card card)
        {
            card.Power = new Power { Trigger = new Trigger(TriggerType.ATTACK) { TriggerSource = TriggerSource.SELF, SingleTask = EffectContext.Task(ctx => {
                var chosen = ctx.PickRandomCard(Sets.CoreCostPools.OtherClassCard(), recipeDbfId: DbfId);
                if (chosen != null) ctx.AddToHand(chosen.Id);
                return TaskState.COMPLETE;
            }) } };
        }
    }
}
