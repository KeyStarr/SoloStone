using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SwampLeech
    {
        internal const int DbfId = 129175;
        internal static void Configure(Card card)
        {
            // Retain the existing power and legacy pool membership (owner decision, 2026-10-21).
            card.Implemented = true;
        }
    }
}
