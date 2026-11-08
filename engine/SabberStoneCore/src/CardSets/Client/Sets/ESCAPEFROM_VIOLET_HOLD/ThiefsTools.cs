using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ThiefsTools
    {
        internal const int DbfId = 126098;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                for (int i = 0; i < 2; i++)
                {
                    var chosen = ctx.PickRandomCard(EscapeFromVioletHoldPools.FourCostSpell(), recipeDbfId: DbfId);
                    if (chosen == null) continue;
                    var generated = Entity.FromCard(c, chosen);
                    generated[GameTag.DISPLAYED_CREATOR] = s.Id;
                    generated.Cost -= 2;
                    Generic.AddHandPhase(c, generated);
                }
            }) };
        }
    }
}
