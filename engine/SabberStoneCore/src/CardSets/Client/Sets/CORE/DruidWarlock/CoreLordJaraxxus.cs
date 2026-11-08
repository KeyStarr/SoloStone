using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CoreLordJaraxxus
    {
        internal const int DbfId = 69637;
        internal static void Configure(Card card)
        {
            // Retain the existing reprint/rebalanced power and legacy pool membership.
            card.Implemented = true;
            card.Tags[(GameTag)858] = 777;
            card.Tags[(GameTag)4533] = 1;
            card.Tags[(GameTag)4546] = 1;
        }
    }
}
