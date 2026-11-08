using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AzsharasTriumph
    {
        internal const int DbfId = 122975;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                for (int i = 0; i < 5; i++)
                {
                    var chosen = ctx.PickRandomCard(CataclysmPools.ExpensiveMinion(), recipeDbfId: DbfId);
                    if (chosen == null || c.DeckZone.IsFull) continue;
                    var generated = Entity.FromCard(c, chosen);
                    generated[GameTag.DISPLAYED_CREATOR] = s.Id;
                    ctx.GiveBuff(generated.Id, "CATA_136e");
                    Generic.ShuffleIntoDeck(c, s, generated);
                }
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(ATK.Effect(EffectOperator.MUL, 2),
                Health.Effect(EffectOperator.MUL, 2)) };
        }
    }
}
