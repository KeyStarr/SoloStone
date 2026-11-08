using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BlastpowderEngineer
    {
        internal const int DbfId = 127016;
        [StandardCard("CAP_104", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Tags[CombatRules.BonusRace] = (int)Race.PIRATE;
            card.Tags[CombatRules.BonusAmount] = 1;
            card.Tags[CombatRules.OwnerTurnOnly] = 1;
        }
    }
}
