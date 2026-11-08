using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DruidOfRegrowth
    {
        internal const int DbfId = 120499;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                for (int i = 0; i < 2; i++)
                {
                    var chosen = ctx.PickRandomCard(TimeTravelPools.NatureSpell(), recipeDbfId: DbfId);
                    if (chosen == null) continue;
                    var spell = (Spell)Entity.FromCard(c, chosen);
                    spell[GameTag.DISPLAYED_CREATOR] = s.Id;
                    if (!HeraldRules.CanAutoCast(spell)) continue;
                    int? target = ctx.RandomLegalTarget(spell.Id);
                    if (target == null && spell.Card.MustHaveTargetToPlay) continue;
                    ctx.CastSpell(spell.Id, HistoryCastKind.Effect, target);
                    g.DeathProcessingAndAuraUpdate();
                }
            }) };
        }
    }
}
