using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    /// <summary>Discover 2 minions. If your deck has no minions, reduce the Cost of any [minions] in your hand by (2).
    /// "Any" means every minion in hand: the launch text (34.0, wiki.gg Solitude patch changes) Discovered a single
    /// minion with the same "any in your hand" clause, and 34.2.2 patch notes changed only the Discover count.</summary>
    internal static class Solitude
    {
        internal const int DbfId = 121023;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(DeckHasNoMinions);
            var discover = new DiscoverThenContinueTask(TimeTravelPools.DiscoverMinion(), recipeDbfId: DbfId);
            var collect = new CustomTask((g, c, s, t, stack) => {
                foreach (var chosen in stack.Playables)
                {
                    Generic.RemoveFromZone(c, chosen);
                    Generic.AddHandPhase(c, chosen);
                }
                stack.Playables.Clear();
            });
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/TIME_448/v1",
                discover, collect, discover, collect,
                new CustomTask((g, c, s, t, stack) => {
                    if (!DeckHasNoMinions((IPlayable)s)) return;
                    foreach (var minion in c.HandZone.OfType<Minion>().ToArray())
                        CostRules.AddModifier(minion, EffectOperator.SUB, 2);
                })) };
        }

        private static bool DeckHasNoMinions(IPlayable source) => !source.Controller.DeckZone.Any(entity => entity is Minion);
    }
}
