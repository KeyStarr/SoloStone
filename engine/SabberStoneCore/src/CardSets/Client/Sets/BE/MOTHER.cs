using System;
using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MOTHER
    {
        internal const int DbfId = 128132;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/BE_036/v1",
                new SelectEntityThenContinueTask(new SelectorSpec(SelectorSource.Hand)),
                EffectContext.Task(ctx => {
                    if (ctx.LastChosenEntityId == 0) return TaskState.COMPLETE;
                    return ctx.RunLegacy(new CustomTask((g, c, s, t, stack) => {
                        var hand = c.HandZone.ToArray();
                        int chosen = Array.FindIndex(hand, p => p.Id == t.Id);
                        if (chosen < 0) return;
                        for (int i = 0; i < hand.Length; i++)
                        {
                            int reduction = 5 - Math.Abs(i - chosen);
                            if (reduction > 0) ctx.GiveBuff(hand[i].Id, "BE_036e2", reduction);
                        }
                    }), ctx.LastChosenEntityId);
                })) };
        }

        internal static void ConfigureDiscount(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.ReduceCost(0)) { UseScriptTag = true } };
        }
    }
}
