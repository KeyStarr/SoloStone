using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DireWolfAlpha
    {
        internal const int DbfId = 69668;
        internal static void Configure(Card card)
        {
            // Retain legacy power and pool membership; restore current export metadata.
            card.Implemented = true;
            card.Tags[(GameTag)858] = 985;
        }
    }
}
