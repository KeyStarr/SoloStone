using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TimelessCausality
    {
        internal const int DbfId = 120977;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var deck = c.DeckZone.GetAll();
                // Draw takes the last entry; SetEntity preserves identities without zone-trigger churn.
                for (int i = 0; i < deck.Length; i++) c.DeckZone.SetEntity(i, deck[deck.Length - 1 - i]);
            }) };
        }
    }
}
