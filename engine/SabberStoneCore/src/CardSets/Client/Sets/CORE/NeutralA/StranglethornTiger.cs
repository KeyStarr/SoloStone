using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class StranglethornTiger
    {
        internal const int DbfId = 69658;
        internal static void Configure(Card card)
        {
            // Retain legacy power and pool membership; restore current export metadata.
            card.Implemented = true;
            card.Tags[(GameTag)858] = 68;
            card.Tags[(GameTag)1584] = 1;
        }
    }
}
