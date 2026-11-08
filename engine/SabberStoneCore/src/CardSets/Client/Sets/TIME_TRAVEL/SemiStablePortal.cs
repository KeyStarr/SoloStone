using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SemiStablePortal
    {
        internal const int DbfId = 118636;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                var chosen = ctx.PickRandomCard(TimeTravelPools.AnyMinion(), recipeDbfId: DbfId);
                if (chosen == null) return;
                var generated = (Playable)Entity.FromCard(c, chosen);
                generated[GameTag.DISPLAYED_CREATOR] = s.Id;
                // No TIME_000 cost enchantment exists in the pinned export.
                CostRules.AddModifier(generated, EffectOperator.SUB, 3);
                Generic.AddHandPhase(c, generated);
            }) };
        }
    }
}
