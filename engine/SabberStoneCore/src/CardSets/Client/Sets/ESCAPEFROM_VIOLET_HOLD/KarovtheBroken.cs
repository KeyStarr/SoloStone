using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class KarovtheBroken
    {
        internal const int DbfId = 126482;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                for (int i = 0; i < 3; i++)
                {
                    var chosen = ctx.PickRandomCard(EscapeFromVioletHoldPools.LegendaryMinion(), recipeDbfId: DbfId);
                    if (chosen == null) continue;
                    var generated = Entity.FromCard(c, chosen);
                    generated[GameTag.DISPLAYED_CREATOR] = s.Id;
                    Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_448e"), (IPlayable)s, generated);
                    Generic.AddHandPhase(c, generated);
                }
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetAttack(1), Effects.SetMaxHealth(1), Effects.SetCost(1)) };
        }
    }
}
