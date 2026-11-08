using SabberStoneCore.Actions;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class KaynSunfury
    {
        internal const int DbfId = 102184;
        [StandardCard("CORE_BT_187", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Tags[CombatRules.IgnoreTauntAura] = 1;
        }
    }
}
