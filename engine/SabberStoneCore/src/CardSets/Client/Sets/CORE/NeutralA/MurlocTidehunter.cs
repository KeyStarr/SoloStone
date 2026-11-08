using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MurlocTidehunter
    {
        internal const int DbfId = 69557;
        internal static void Configure(Card card)
        {
            // Retain legacy power and pool membership; restore current export metadata.
            card.Implemented = true;
            card.Tags[(GameTag)858] = 976;
            card.Tags[(GameTag)1452] = 1078;
        }
    }
}
