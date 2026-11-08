using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BloodClone
    {
        internal const int DbfId = 126503;
        internal static readonly OfflinePoolFilter Filter = OfflinePoolFilter.Discover(type: CardType.MINION, minCost: 5, maxCost: 5);
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(CanSummonCopy);
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/JAIL_451/v1",
                new DiscoverThenContinueTask(Filter, recipeDbfId: DbfId),
                new CustomTask((g, c, s, t, stack) => {
                    var chosen = stack.Playables.SingleOrDefault();
                    if (chosen == null) return;
                    Generic.RemoveFromZone(c, chosen);
                    Generic.AddHandPhase(c, chosen);
                    if (CanSummonCopy((IPlayable)s) && CorpseRules.TrySpend(c, 5))
                        new EffectContext(g, c.Id, s.Id, stack: stack).Summon(chosen.Card.Id);
                })) };
        }

        private static bool CanSummonCopy(IPlayable source) =>
            source.Game.EnableCorpses && CorpseRules.Available(source.Controller) >= 5;
    }
}
