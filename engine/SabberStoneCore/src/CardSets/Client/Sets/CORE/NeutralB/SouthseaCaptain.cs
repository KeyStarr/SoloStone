using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SouthseaCaptain
    {
        internal const int DbfId = 69679;
        internal static void Configure(Card card)
        {
            // Retain the existing power and legacy pool membership (owner decision, 2026-10-21).
            card.Implemented = true;
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Text = "+1/+1.";
        }
    }
}
