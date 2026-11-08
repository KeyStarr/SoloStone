using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BygoneEchoes
    {
        internal const int DbfId = 121060;
        [StandardCard("END_005", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(CanSpend);
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                bool outcast = OutcastRules.PlayedFromEdge((IPlayable)s);
                var ctx = new EffectContext(g,c.Id,s.Id,stack: stack);
                Summon(ctx,c);
                // Unpinned fixed-spend/full-board policy: the entire affordable bonus is paid once even without capacity.
                if (CorpseRules.TrySpend(c,4)) Summon(ctx,c);
                if (outcast) Summon(ctx,c);
            }) };
        }
        private static bool CanSpend(IPlayable s) => CorpseRules.Available(s.Controller)>=4;
        private static void Summon(EffectContext ctx, Controller owner)
        {
            if (owner.BoardZone.IsFull) return;
            var chosen = ctx.PickRandomCard(B14H4TimePools.Four(),recipeDbfId:DbfId);
            if (chosen != null) ctx.Summon(chosen.Id);
        }
    }
}
