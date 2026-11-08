using System.Linq;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AqirGoliath
    {
        internal const int DbfId = 130115;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { // PlayCard records printed cards; Game clears the list at turn end.
                Aura = new AdaptiveCostEffect(p => 2 * p.Controller.CardsPlayedThisTurn.Count(c => c.Combo)),
                PowerTask = new DrawTask() };
        }
    }
}
