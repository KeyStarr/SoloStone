using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BralmaSearstone
    {
        internal const int DbfId = 117617;
        [StandardCard("TLC_228", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Tags[CombatRules.BonusRace] = (int)Race.ELEMENTAL;
            card.Tags[CombatRules.BonusAmount] = 1;
        }
    }
}
