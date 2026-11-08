using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ShieldBlock
    {
        internal const int DbfId = 76302;
        internal static void Configure(Card card)
        {
            // Existing legacy power and pool membership are retained (owner decision, 2026-10-21).
            card.Implemented = true;
            card.Tags[(GameTag)201] = 3;
            card.Tags[(GameTag)858] = 1023;
            card.Tags[(GameTag)2589] = 1;
        }
    }
}
