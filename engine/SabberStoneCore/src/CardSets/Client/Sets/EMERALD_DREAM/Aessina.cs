using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Aessina
    {
        internal const int DbfId = 113700;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.PoweredUpWhen(TwentyFriendlyDeaths);
            // Rule: count friendly board deaths across all turns, including before creation; discard and enemy deaths do not count.
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (TwentyFriendlyDeaths((IPlayable)s))
                    SplitHits.Inline(20, ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 1), deathProcessing: false).Process(g, c, s, t, stack);
            }) };
        }

        private static bool TwentyFriendlyDeaths(IPlayable source) => source.Game.HistoryFacts.Query.Deaths()
            .Count(f => f.ControllerId == source.Controller.Id && f.EntityId != source.Id) >= 20;
    }
}
