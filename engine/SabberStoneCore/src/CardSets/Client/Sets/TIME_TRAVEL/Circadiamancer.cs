using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Circadiamancer
    {
        internal const int DbfId = 121671;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                var chosen = ctx.PickRandomCard(TimeTravelPools.EightCostMinion(), recipeDbfId: DbfId);
                if (chosen == null) return;
                var generated = Entity.FromCard(c, chosen, new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                Generic.AddHandPhase(c, generated);
                if (generated.Zone == c.HandZone)
                    Generic.AddEnchantmentBlock(g, Cards.FromId("TIME_102e"), (IPlayable)s, generated);
            }) };
        }

        internal static void ConfigureDiscount(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_START) {
                SingleTask = new CustomTask((g, c, s, t, stack) => {
                    var generated = ((Enchantment)s).Target as Playable;
                    if (generated?.Zone == c.HandZone)
                        CostRules.AddModifier(generated, EffectOperator.SUB, 1);
                })
            } };
        }
    }
}
