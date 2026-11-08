using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Core2026
{
    internal static class BigGameHunter
    {
        internal const int DbfId = 69652;
        internal static void Configure(Card card)
        {
            // Battlecry is unchanged; client tags supply current stats and Tradeable.
            card.ExcludeFromLegacyRandomPools = true;
        }
    }
}
