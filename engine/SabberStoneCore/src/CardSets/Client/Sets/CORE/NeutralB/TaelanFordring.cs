using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TaelanFordring
    {
        internal const int DbfId = 66856;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var minions = c.DeckZone.Where(x => x.Card.Type == CardType.MINION).ToArray();
                if (minions.Length == 0) return;
                int highest = minions.Max(x => x.Cost);
                Generic.Draw(c, minions.Where(x => x.Cost == highest).ToArray().Choose(g.Random));
            }) };
        }
    }
}
