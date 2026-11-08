using SabberStoneCore.Actions;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BulwarkOfAzzinoth
    {
        internal const int DbfId = 116718;
        [StandardCard("CORE_BT_781", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Tags[CombatRules.ReplaceHeroDamage] = 1;
        }
    }
}
