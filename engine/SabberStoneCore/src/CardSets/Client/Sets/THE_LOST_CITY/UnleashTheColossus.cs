using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class UnleashTheColossus
    {
        internal const int DbfId = 117871;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            QuestDefinition.Register(new QuestDefinition(DbfId, 12, QuestRewardKind.CardToHand, "TLC_631t"));
            var advance = EffectContext.Task(ctx => { ctx.AdvanceQuest(); return TaskState.COMPLETE; });
            advance.IsTrigger = true;
            card.Power = new Power { PowerTask = advance };
            QuestRules.RegisterDamageListener(DbfId, (listener, fact) =>
                fact.Amount == 2 && fact.SourceControllerId == listener.Controller.Id
                    && fact.TargetControllerId != fact.SourceControllerId && fact.TurnControllerId == fact.SourceControllerId
                    ? listener.Card.Power.PowerTask : null);
        }
    }
}
