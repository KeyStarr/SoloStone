using SabberStoneCore.Model;
using SabberStoneCore.Enums;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BoulderfistOgre
    {
        internal const int DbfId = 112924;
        internal static void Configure(Card card)
        {
            // Retain the existing power and legacy pool membership (owner decision, 2026-10-21).
            card.Implemented = true;
            card.Text = "";
            card.Tags[(GameTag)858] = 1686;
        }
    }
}
