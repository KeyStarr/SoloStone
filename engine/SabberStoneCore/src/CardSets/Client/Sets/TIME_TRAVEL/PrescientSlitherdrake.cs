using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PrescientSlitherdrake
    {
        internal const int DbfId = 121196;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(TimeTravelHandConditions.HoldingAnotherDragon);
            card.Power = new Power { Aura = new AdaptiveCostEffect(p =>
                TimeTravelHandConditions.HoldingAnotherDragon(p) ? 3 : 0) };
        }
    }
}
