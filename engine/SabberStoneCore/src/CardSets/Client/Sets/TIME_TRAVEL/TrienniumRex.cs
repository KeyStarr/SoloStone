using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    /// <summary>Kindred and Deathrattle: get a random (any-class) Deathrattle minion that costs (2) less.</summary>
    internal static class TrienniumRex
    {
        internal const int DbfId = 121157;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                PowerTask = new CustomTask((g, c, s, t, stack) => {
                    int repeats = KindredRules.BeginMatchedClause((IPlayable)s);
                    for (int i = 0; i < repeats; i++) Get(g, c, s, stack);
                }),
                DeathrattleTask = new CustomTask((g, c, s, t, stack) => Get(g, c, s, stack))
            };
        }

        private static void Get(Game g, Controller c, IEntity s, TaskStack stack)
        {
            var chosen = new EffectContext(g, c.Id, s.Id, stack: stack)
                .PickRandomCard(TimeTravelPools.DeathrattleMinion(), recipeDbfId: DbfId);
            if (chosen == null) return;
            var generated = (Playable)Entity.FromCard(c, chosen);
            generated[GameTag.DISPLAYED_CREATOR] = s.Id;
            // The export has no enchantment for this discount: a permanent cost modifier on the generated entity.
            CostRules.AddModifier(generated, EffectOperator.SUB, 2);
            Generic.AddHandPhase(c, generated);
        }
    }
}
