using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CrazedAlchemist
    {
        internal const int DbfId = 69661;
        internal static void Configure(Card card)
        {
            // Retain legacy power and pool membership; restore current export metadata.
            card.Implemented = true;
            card.Tags[(GameTag)858] = 801;
            card.Tags[(GameTag)1440] = 1;
        }
    }
}
