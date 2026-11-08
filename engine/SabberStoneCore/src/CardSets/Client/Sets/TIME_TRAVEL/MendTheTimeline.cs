using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MendTheTimeline
    {
        internal const int DbfId = 119977;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                var spells = new List<IPlayable>(2);
                for (int i = 0; i < 2; i++)
                {
                    var chosen = ctx.PickRandomCard(TimeTravelPools.HolySpell(), recipeDbfId: DbfId);
                    if (chosen == null) continue;
                    var generated = Entity.FromCard(c, chosen);
                    generated[GameTag.DISPLAYED_CREATOR] = s.Id;
                    Generic.AddHandPhase(c, generated);
                    spells.Add(generated);
                }
                g.AuraUpdate();
                // Unpinned_FullHandCosts: burned generated spells also contribute their current Costs.
                ctx.Heal(c.Hero.Id, spells.Sum(spell => spell.Cost));
            }) };
        }
    }
}
