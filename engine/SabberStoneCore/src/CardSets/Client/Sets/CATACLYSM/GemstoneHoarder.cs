using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GemstoneHoarder
    {
        internal const int DbfId = 123660;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/CATA_897/v1",
                new SelectEntityThenContinueTask(),
                EffectContext.Task(ctx => ctx.LastChosenEntityId == 0 ? TaskState.COMPLETE :
                    ctx.RunLegacy(new CustomTask((g, c, s, t, stack) => {
                        if (s.Zone == c.BoardZone)
                            Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_897e3"),
                                (IPlayable)s, s, entityId: t.Id);
                        Generic.DiscardBlock.Invoke(c, t);
                    }), ctx.LastChosenEntityId))) };
        }

        internal static void ConfigureCapture(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { DeathrattleTask = ComplexTask.Create(
                GetCapturedCardTask.Task,
                new ApplyEffectTask(EntityType.STACK, Effects.ReduceCost(1)),
                new AddStackTo(EntityType.HAND)) };
        }
    }
}
