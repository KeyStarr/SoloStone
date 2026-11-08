using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class LivingGarden
    {
        internal const int DbfId = 114289;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Lean_ConsumerClauseOrder: imbue-16; pinned export has no EDR_518 enchantment.
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/EDR_518/v1",
                EffectContext.Task(ctx => { ctx.Imbue(); return TaskState.COMPLETE; }),
                new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.Hand,
                    side: SelectorSide.Friendly, cardType: CardType.MINION, ifEmpty: EmptySelectorAction.Continue)),
                EffectContext.Task(ctx => {
                    if (ctx.LastChosenEntityId == 0) return TaskState.COMPLETE;
                    return ctx.RunLegacy(new CustomTask((g, c, s, t, stack) =>
                        CostRules.AddModifier((Playable)t, EffectOperator.SUB, 1)), ctx.LastChosenEntityId);
                })) };
        }
    }
}
