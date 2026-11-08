using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TolvirCarver
    {
        internal const int DbfId = 122961;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/CATA_566/v1",
                new SelectEntityThenContinueTask(),
                SabberStoneCore.EffectSystem.EffectContext.Task(ctx => {
                    if (ctx.LastChosenEntityId == 0) return SabberStoneCore.Tasks.TaskState.COMPLETE;
                    return ctx.RunLegacy(new AddEnchantmentTask("CATA_566e", EntityType.TARGET), ctx.LastChosenEntityId);
                })) };
        }
        internal static void ConfigureCarving(Card card)
        {
            card.Implemented = true; card.CopyHandTrigger = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_START) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var held = (IPlayable)((Enchantment)s).Target;
                    if (held.Zone == held.Controller.HandZone)
                        CostRules.AddModifier((Playable)held, EffectOperator.SUB, 1);
                })
            } };
        }
    }
}
