using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SmuggledShovel
    {
        internal const int DbfId = 125996;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            // Rule: initial entity membership survives in-place transforms/reshuffles; new same-ID copies remain foreign (history-facts §7).
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var spells = c.DeckZone.Where(p => p.Card.Type == CardType.SPELL
                    && !g.HistoryFacts.Query.StartedInDeck(p.Id, c.Id)).ToArray();
                if (spells.Length != 0) Generic.Draw(c, spells[g.Random.Next(spells.Length)]);
            }) };
        }
    }
}
