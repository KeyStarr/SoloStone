using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GlacialShard
    {
        internal const int DbfId = 102227;
        internal static void Configure(Card card)
        {
            // Retain the existing power and legacy pool membership (owner decision, 2026-10-21).
            card.Implemented = true;
        }
    }
}
