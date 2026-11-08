using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class LootHoarder
    {
        internal const int DbfId = 69665;
        internal static void Configure(Card card)
        {
            // Existing legacy power and pool membership are retained (owner decision, 2026-10-21).
            card.Implemented = true;
            card.Tags[(GameTag)201] = 3;
            card.Tags[(GameTag)251] = 6;
            card.Tags[(GameTag)858] = 251;
        }
    }
}
