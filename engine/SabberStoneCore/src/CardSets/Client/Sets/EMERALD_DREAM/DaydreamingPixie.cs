using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DaydreamingPixie
    {
        internal const int DbfId = 115364;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_END) {
                SingleTask = EffectContext.Task(ctx => {
                    var chosen = ctx.PickRandomCard(OfflinePoolDeclarations.NatureSpell(), recipeDbfId: DbfId);
                    if (chosen != null) ctx.AddToHand(chosen.Id);
                    return TaskState.COMPLETE;
                })
            } };
        }
    }
}
