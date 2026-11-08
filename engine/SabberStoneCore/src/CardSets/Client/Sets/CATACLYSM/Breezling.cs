using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Breezling
    {
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.Power = new Power();
        }
    }
}
