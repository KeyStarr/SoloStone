using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using System;
using SabberStoneCore.EffectSystem;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HallsofOrigination
    {
        internal const int DbfId = 129839;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = ComplexTask.Create(new CustomTask((g, c, s, t, stack) => {
                int missiles = (8 + SabberStoneCore.Actions.SpellDamageRules.Bonus((SabberStoneCore.Model.Entities.IPlayable)s))
                    * (int)Math.Pow(2, c.ControllerAuraEffects[GameTag.SPELLPOWER_DOUBLE]);
                SplitHits.Inline(g, c, s, missiles,
                    ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 1), deathProcessing: true);
            }), new EnqueueTask(1, EffectContext.Task(ctx => {
                var chosen = ctx.PickRandomCard(BEPools.EightCostMinion(), recipeDbfId: DbfId);
                if (chosen != null) ctx.Summon(chosen.Id);
                return TaskState.COMPLETE;
            }))) };
        }
    }
}
