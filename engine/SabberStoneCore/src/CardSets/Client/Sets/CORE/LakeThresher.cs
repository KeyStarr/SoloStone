using SabberStoneCore.Actions;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class LakeThresher
    {
        internal const int DbfId = 129176;
        [StandardCard("CORE_SCH_605", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Tags[CombatRules.Cleave] = 1;
        }
    }
}
