using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Core2026
{
    internal static class TheBlackKnight
    {
        internal const int DbfId = 102217;
        internal static void Configure(Card card)
        {
            // Battlecry is unchanged; client tags supply current stats, Undead and Tradeable.
            card.ExcludeFromLegacyRandomPools = true;
        }
    }
}
