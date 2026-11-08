using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ForgottenMillennium
    {
        internal const int DbfId = 121014;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                int slots = c.HandZone.FreeSpace;
                for (int i = 0; i < slots; i++)
                {
                    var definition = ctx.PickRandomCard(TimeTravelCostPools.Undead(), recipeDbfId: DbfId);
                    if (definition == null) continue;
                    var generated = (Playable)Entity.FromCard(c, definition,
                        new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id }, zone: c.HandZone);
                    var actor = Enchantment.GetInstance(c, (IPlayable)s, generated, Cards.FromId("TIME_615e"));
                    CostRules.AddPaymentOverride(generated, CostPaymentKind.Health, g.Turn, actor.Id);
                }
            }) };
        }
    }
}
