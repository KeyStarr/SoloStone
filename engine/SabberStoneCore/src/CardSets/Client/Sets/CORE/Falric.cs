using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Falric
    {
        [StandardCard("CORE_EDR_003", 118765)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var matches = c.DeckZone.Where(e => e.Card[GameTag.CORPSE_SPENDER] != 0).ToArray();
                if (matches.Length == 0) return;
                var selected = matches[g.Random.Next(matches.Length)];
                g.OnRandomHappened(true);
                Generic.Draw(c, selected);
            }) };
        }
    }
}
