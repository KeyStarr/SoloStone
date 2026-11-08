using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class IntertwinedFate
    {
        [StandardCard("TIME_432", 120477)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("b18/intertwined-fate/v1",
                new DiscoverThenContinueTask(DiscoverEntitySource.Deck), new CustomTask(Copy),
                new DiscoverThenContinueTask(DiscoverEntitySource.EnemyDeck), new CustomTask(Copy)) };
        }
        private static void Copy(Game g, Controller c, IEntity s, IPlayable t, TaskStack stack)
        {
            var selected = stack.Playables?.LastOrDefault();
            if (selected != null) Generic.Copy(c, s, selected, Zone.HAND);
        }
    }
}
