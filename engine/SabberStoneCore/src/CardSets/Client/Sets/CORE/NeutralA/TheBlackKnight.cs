using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TheBlackKnight
    {
        internal const int DbfId = 102217;
        internal static void Configure(Card card)
        {
            // Retain legacy power and pool membership; restore current export metadata.
            card.Implemented = true;
            card.Tags[(GameTag)858] = 1656;
            card.Tags[(GameTag)1720] = 1;
            card.Tags[(GameTag)1743] = 1;
            card.Tags[(GameTag)2589] = 1;
        }
    }
}
