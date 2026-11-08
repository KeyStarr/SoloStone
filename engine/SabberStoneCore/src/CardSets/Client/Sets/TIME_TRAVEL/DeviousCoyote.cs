using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DeviousCoyote
    {
        internal const int DbfId = 120859;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            // Rule: count positive enemy-hero damage events, including armor and any source, on this global turn (design R5).
            card.Power = new Power { Aura = new AdaptiveCostEffect(p =>
                p.Game.HistoryFacts.Query.HeroDamageEventCount(p.Controller.Opponent.Id, p.Game.HistoryFacts.Query.ThisTurn)) };
        }
    }
}
