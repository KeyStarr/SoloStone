using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GravedawnVoidbulb
    {
        internal const int DbfId = 117316;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                bool kindred = KindredRules.Match((IPlayable)s);
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                Summon(ctx, c);
                int repeats = kindred ? KindredRules.BeginMatchedClause((IPlayable)s) : 0;
                for (int i = 0; i < repeats; i++) Summon(ctx, c);
            }) };
        }

        private static void Summon(EffectContext ctx, Controller controller)
        {
            if (controller.BoardZone.IsFull) return;
            var chosen = ctx.PickRandomCard(TheLostCityPools.FourCostMinion(), recipeDbfId: DbfId);
            if (chosen == null) return;
            int summoned = ctx.Summon(chosen.Id);
            if (summoned != 0) ctx.GiveBuff(summoned, "TLC_815e");
        }

        internal static void ConfigureTaunt(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(new Effect(GameTag.TAUNT, EffectOperator.SET, 1)) };
        }
    }
}
