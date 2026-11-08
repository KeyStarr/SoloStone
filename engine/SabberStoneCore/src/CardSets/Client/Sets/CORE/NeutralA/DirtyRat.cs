using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DirtyRat
    {
        internal const int DbfId = 102225;
        internal static void Configure(Card card)
        {
            // Retain legacy power and pool membership; restore current export metadata.
            card.Implemented = true;
            card.Tags[(GameTag)858] = 41567;
            card.Tags[(GameTag)2589] = 1;
            card.Tags[(GameTag)4533] = 1;
            card.Tags[(GameTag)4547] = 1;
        }
    }
}
