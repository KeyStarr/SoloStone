using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FuturisticForefather
    {
        internal const int DbfId = 120784;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/TIME_041/v1",
                new DiscoverThenContinueTask(DiscoverEntitySource.EnemyHandGuess, fallback: TimeTravelPools.GuessFallback(), recipeDbfId: DbfId),
                new CustomTask((g, c, s, t, stack) => {
                    if (stack.Playables?.LastOrDefault()?[DiscoverRealRules.CorrectAnswer] == 1)
                        Generic.AddEnchantmentBlock(g, Cards.FromId("TIME_041e2"), (IPlayable)s, (IPlayable)s);
                })) };
        }
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Health_N(4)) };
        }
    }
}
