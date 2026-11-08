using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enums;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RotheartDryad
    {
        internal const int DbfId = 115039;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = EffectContext.Task(ctx => ctx.RunLegacy(ComplexTask.DrawFromDeck(1, SelfCondition.IsMinion, SelfCondition.IsCost(7, RelaSign.GEQ)))) };
        }
    }
}
