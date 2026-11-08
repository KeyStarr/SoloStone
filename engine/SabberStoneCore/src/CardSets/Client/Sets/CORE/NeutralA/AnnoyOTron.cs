using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AnnoyOTron
    {
        internal const int DbfId = 69718;
        internal static void Configure(Card card)
        {
            // Existing legacy power and pool membership are retained (owner decision, 2026-10-21).
            card.Implemented = true;
            card.Tags[(GameTag)858] = 2053;
            card.Tags[(GameTag)1440] = 2;
            card.Tags[(GameTag)2589] = 1;
        }
    }
}
