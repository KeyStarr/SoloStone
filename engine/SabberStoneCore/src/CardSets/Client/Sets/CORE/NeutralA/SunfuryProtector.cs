using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SunfuryProtector
    {
        internal const int DbfId = 120578;
        internal static void Configure(Card card)
        {
            // Retain legacy power and pool membership; restore current export metadata.
            card.Implemented = true;
            card.Tags[(GameTag)858] = 891;
            card.Tags[(GameTag)1413] = 891;
            card.Tags[(GameTag)1576] = 1;
        }
    }
}
