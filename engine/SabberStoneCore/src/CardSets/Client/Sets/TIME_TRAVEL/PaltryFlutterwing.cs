using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PaltryFlutterwing
    {
        internal const int DbfId = 120964;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                if (c.BoardZone.IsFull) return;
                var chosen = new EffectContext(g, c.Id, s.Id).PickRandomCard(TimeTravelPools.TwoCostMinion(), recipeDbfId: DbfId);
                if (chosen == null) return;
                var minion = (Minion)Entity.FromCard(c, chosen, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                DormantRules.MarkPending(minion, 2, "TIME_058e");
                Generic.SummonBlock(g, minion, EffectContext.DeathrattlePosition(c, s), s);
            }) };
        }
    }
}
