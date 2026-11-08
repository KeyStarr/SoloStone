using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BootlegAlchemist
    {
        internal const int DbfId = 126111;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/JAIL_313/v1",
                new SelectEntityThenContinueTask(),
                EffectContext.Task(ctx => ctx.LastChosenEntityId == 0 ? TaskState.COMPLETE :
                    ctx.RunLegacy(new CustomTask((g, c, s, t, stack) => {
                        int cost = t.Cost;
                        stack ??= new TaskStack();
                        stack.Number = cost + 5;
                        var chosen = new EffectContext(g, c.Id, s.Id, t.Id, stack)
                            .PickRandomCard(EscapeFromVioletHoldPools.Spell(), PoolCostBinding.StackNumber, DbfId);
                        if (chosen == null) return;
                        // A Shatter parent splits when this scope closes, so its halves inherit the kept Cost.
                        using (c.HandZone.BeginCardStateMutation())
                        {
                            var transformed = Generic.ChangeEntityBlock(c, t, chosen, true);
                            Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_313e"), (IPlayable)s, transformed, cost);
                        }
                    }), ctx.LastChosenEntityId))) };
        }

        internal static void ConfigureCost(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetCost(0)) { UseScriptTag = true } };
        }
    }
}
