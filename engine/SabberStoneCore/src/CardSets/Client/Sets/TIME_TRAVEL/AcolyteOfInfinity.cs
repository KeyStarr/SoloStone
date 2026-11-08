using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AcolyteOfInfinity
    {
        [StandardCard("END_018", 121162)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                PowerTask = new CustomTask((g,c,s,t,stack) => InfinityRules.MarkCost((IPlayable)s)),
                DeathrattleTask = new CustomTask((g,c,s,t,stack) => InfinityRules.RestoreCost(s))
            };
        }
        [StandardCard("END_018e", 121163)]
        internal static void Mark(Card card) { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.Power = new Power(); }
        [StandardCard("END_018e2", 121164)]
        internal static void Restored(Card card) { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.Power = new Power(); }
    }
}
