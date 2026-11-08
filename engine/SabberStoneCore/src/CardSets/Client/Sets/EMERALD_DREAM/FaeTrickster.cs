using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enums;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FaeTrickster
    {
        internal const int DbfId = 116672;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = EffectContext.Task(ctx => ctx.RunLegacy(ComplexTask.DrawFromDeck(1, SelfCondition.IsSpell, SelfCondition.IsCost(5, RelaSign.GEQ)))) };
        }
    }
}
