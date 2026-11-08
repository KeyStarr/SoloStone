using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class KingMukla
    {
        internal const int DbfId = 69656;
        internal static void Configure(Card card)
        {
            // Retain legacy power and pool membership; restore current export metadata.
            card.Implemented = true;
            card.Tags[(GameTag)858] = 1693;
            card.Tags[(GameTag)1452] = 1694;
        }
    }
}
