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
    internal static class DreadLeviathan
    {
        internal const int DbfId = 123539;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            var choose = new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.Board,
                SelectorSide.Enemy, cardType: CardType.MINION));
            var steal = EffectContext.Task(ctx => {
                if (ctx.LastChosenEntityId == 0) return TaskState.COMPLETE;
                return ctx.RunLegacy(new CustomTask((g, c, s, t, stack) => {
                    // A steal of 3, capped at the victim's current Health; the victim keeps its damage, so a damaged minion can die.
                    int amount = HealthLossRules.StealAmount((Minion)t, 3);
                    if (amount > 0)
                    {
                        ctx.GiveBuff(t.Id, "CATA_699e", amount);
                        ctx.GiveBuff(s.Id, "CATA_699e2", amount);
                    }
                    HealthLossRules.MarkIfDepleted((Minion)t);
                    g.DeathProcessingAndAuraUpdate();
                }), ctx.LastChosenEntityId);
            });
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/CATA_699/v1",
                choose, steal, choose, steal, choose, steal) };
        }
        internal static void ConfigureDrain(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Health.Effect(EffectOperator.SUB, 0)) { UseScriptTag = true } };
        }
        internal static void ConfigureGain(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Health_N(0)) { UseScriptTag = true } };
        }
    }
}
