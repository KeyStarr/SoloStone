using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class JailhouseManastorm
    {
        internal const int DbfId = 126353;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = EffectContext.Task(ctx => {
                ctx.AddPlayerEffect("JAIL_122e5");
                return TaskState.COMPLETE;
            }) };
        }

        internal static void ConfigureSummoning(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_CAST) {
                TriggerSource = TriggerSource.FRIENDLY,
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    stack ??= new TaskStack();
                    stack.Number = t[GameTag.TAG_LAST_KNOWN_COST_IN_HAND];
                    var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                    int count = s[GameTag.TAG_SCRIPT_DATA_NUM_1];
                    for (int i = 0; i < count && !c.BoardZone.IsFull && g.State != State.COMPLETE; i++)
                    {
                        var chosen = ctx.PickRandomCard(EscapeFromVioletHoldPools.ManastormMinion(), PoolCostBinding.StackNumber, DbfId);
                        if (chosen != null) ctx.Summon(chosen.Id);
                    }
                })
            } };
        }
    }
}
