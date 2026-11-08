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
    internal static class WhisperingStone
    {
        internal const int DbfId = 118277;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                for (int i = 0; i < 2; i++)
                {
                    var definition = ctx.PickRandomCard(TheLostCityCostPools.FelSpell(), recipeDbfId: DbfId);
                    if (definition == null) continue;
                    var spell = (Playable)Entity.FromCard(c, definition);
                    spell[GameTag.DISPLAYED_CREATOR] = s.Id;
                    var actor = Enchantment.GetInstance(c, (IPlayable)s, spell, Cards.FromId("TLC_467e"));
                    CostRules.AddPaymentOverride(spell, CostPaymentKind.Health, enchantmentId: actor.Id);
                    Generic.AddHandPhase(c, spell);
                }
            }) };
        }
    }
}
