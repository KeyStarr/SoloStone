using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CultNeophyte
    {
        internal const int DbfId = 102224;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var tax = Cards.FromId("SCH_713e");
                var enchantment = Enchantment.GetInstance(c, (IPlayable)s, c.Opponent, tax);
                // The export marks this as one-turn; Loatheb's aura owns the later expiry instead.
                g.OneTurnEffectEnchantments.Remove(enchantment);
                tax.Power.Aura.Activate(enchantment);
            }) };
        }

        internal static void ConfigureTax(Card card)
        {
            card.Implemented = true;
            // Loatheb's caster-owned controller enchantment expires at the opponent's turn end.
            card.Power = new Power { Aura = new Aura(AuraType.OP_HAND, Effects.AddCost(1)) {
                Condition = SelfCondition.IsSpell,
                RemoveTrigger = (TriggerType.TURN_END, SelfCondition.IsOpTurn)
            } };
        }
    }
}
