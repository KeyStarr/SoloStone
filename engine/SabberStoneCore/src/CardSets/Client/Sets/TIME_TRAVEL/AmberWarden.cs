using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    /// <summary>Taunt. Deathrattle: summon a random minion from the Past pool into the dying minion's slot.</summary>
    internal static class AmberWarden
    {
        internal const int DbfId = 120951;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                var chosen = ctx.PickRandomCard(TimeTravelPools.PastMinion(), recipeDbfId: DbfId);
                if (chosen != null) ctx.Summon(chosen.Id, EffectContext.DeathrattlePosition(c, s));
            }) };
        }
    }
}
