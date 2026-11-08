using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DiveTheGolakkaDepths
    {
        internal const int DbfId = 117341;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            QuestDefinition.Register(new QuestDefinition(DbfId, 6, QuestRewardKind.PlayerEffect, "TLC_426e", repeatable: true));
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_SUMMON) {
                TriggerSource = TriggerSource.MINIONS,
                Condition = new SelfCondition(p => p.Card.IsRace(Race.MURLOC)),
                SingleTask = EffectContext.Task(ctx => { ctx.AdvanceQuest(); return TaskState.COMPLETE; })
            } };
        }

        internal static void ConfigureReward(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_SUMMON) {
                TriggerSource = TriggerSource.MINIONS,
                Condition = new SelfCondition(p => p.Card.IsRace(Race.MURLOC)),
                // q1 pins the old-stack outcome; FastExecution ordering remains wire-unpinned.
                FastExecution = true,
                SingleTask = EffectContext.Task(ctx => {
                    if (ctx.TargetId is int target)
                        ctx.GiveBuff(target, "TLC_426e2", ctx.PlayerEffectStacks("TLC_426e"));
                    return TaskState.COMPLETE;
                })
            } };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(0), Effects.Health_N(0)) { UseScriptTag = true } };
        }
    }
}
