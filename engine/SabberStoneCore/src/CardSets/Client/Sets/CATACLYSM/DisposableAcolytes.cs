using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DisposableAcolytes
    {
        internal const int DbfId = 122831;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                PowerTask = EffectContext.Task(Summon),
                Trigger = new Trigger(TriggerType.DISCARD) {
                    TriggerActivation = TriggerActivation.HAND,
                    TriggerSource = TriggerSource.SELF,
                    SingleTask = EffectContext.Task(Summon)
                }
            };
        }
        private static TaskState Summon(EffectContext ctx)
        {
            for (int i = 0; i < 2; i++)
            {
                var chosen = ctx.PickRandomCard(CataclysmPools.CostMinion(1), recipeDbfId: DbfId);
                if (chosen != null) ctx.Summon(chosen.Id);
            }
            return TaskState.COMPLETE;
        }
    }
}
