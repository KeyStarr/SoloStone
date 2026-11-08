using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RafaamLadder
    {
        internal const int DbfId = 120490;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var costs = new HashSet<int>();
                for (int i = 0; i < 3; i++)
                {
                    var selected = new TaskStack { Playables = c.DeckZone.Where(cardInDeck => !costs.Contains(cardInDeck.Cost)).ToArray() };
                    if (selected.Playables.Count == 0) break;
                    new RandomTask(1, EntityType.STACK).Process(g, c, s, null, selected);
                    var drawn = selected.Playables[0];
                    costs.Add(drawn.Cost);
                    Generic.Draw(c, c.DeckZone.ToList().IndexOf(drawn));
                }
            }) };
        }
    }
}
