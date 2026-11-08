using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BlessingOfTheMoon
    {
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new System.Collections.Generic.Dictionary<PlayReq, int> { { PlayReq.REQ_HAND_NOT_FULL, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                int n = s[GameTag.TAG_SCRIPT_DATA_NUM_1], remaining = c.RemainingMana;
                // Lean_ imbue-20: printed affordability after power payment, without target/space checks.
                if (remaining < 0) return;
                var bound = new TaskStack { Number = (int)Math.Min(int.MaxValue, (long)n + remaining) };
                var ctx = new EffectContext(g, c.Id, s.Id, stack: bound);
                var offers = new[] {
                    ctx.PickRandomCard(EmeraldDreamPools.MoonMinion(), PoolCostBinding.StackNumberMaxCost, recipeDbfId: 114069),
                    ctx.PickRandomCard(EmeraldDreamPools.MoonSpell(), PoolCostBinding.StackNumberMaxCost, recipeDbfId: 114069)
                }.Where(candidate => candidate != null).ToArray();
                // Lean_ imbue-21/22: normal choice even with one offer/full hand; discount before Temporary/hand entry.
                if (!ctx.Discover(offers, ChoiceAction.STACK, new MoonGrantTask(n))) return;
                var choice = c.Choice;
                while (choice.NextChoice != null) choice = choice.NextChoice;
                foreach (int id in choice.Choices)
                    g.IdEntityDic[id][Generic.PrivateDecisionOfferTag] = c.PlayerId;
            }) };
        }
    }
}
