using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CoreFlameImp
    {
        internal const int DbfId = 69636;
        internal static void Configure(Card card)
        {
            // Retain the existing reprint/rebalanced power and legacy pool membership.
            card.Implemented = true;
            card.Tags[(GameTag)858] = 1090;
            card.Tags[(GameTag)1965] = 1;
            card.Tags[(GameTag)4533] = 1;
            card.Tags[(GameTag)4547] = 1;
        }
    }
}
