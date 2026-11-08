using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TheEverbloom
    {
        internal const int DbfId = 117662;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_ATTACK) {
                TriggerSource = TriggerSource.HERO,
                SingleTask = EffectContext.Task(ctx => {
                    foreach (int id in ctx.FriendlyMinionIds) ctx.GiveBuff(id, "TLC_239e");
                    return TaskState.COMPLETE;
                })
            } };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(2), Effects.Health_N(2)) };
        }
    }
}
