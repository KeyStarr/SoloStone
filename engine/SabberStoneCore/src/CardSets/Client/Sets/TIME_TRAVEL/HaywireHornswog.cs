using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HaywireHornswog
    {
        internal const int DbfId = 121193;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            // Generic.OverloadBlock updates this controller tag for spells, minions and weapons.
            card.Power = new Power { Aura = new AdaptiveCostEffect(p => p.Controller.OverloadThisGame) };
        }
    }
}
