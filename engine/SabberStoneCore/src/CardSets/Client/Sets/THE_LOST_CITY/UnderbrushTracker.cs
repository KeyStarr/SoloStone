using System.Linq;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class UnderbrushTracker
    {
        internal const int DbfId = 117702;
        // Rule: count successful gameplay shuffle operations by you into your deck, not individual cards (history-facts R4).
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.ConsumesHistory = true;
            card.Power = new Power { Aura = new AdaptiveCostEffect(ShuffleCount) };
        }

        internal static int ShuffleCount(IPlayable source) => source.Game.HistoryFacts.Query.Events<ShuffleOperationFact>()
            .Where(f => f.ControllerId == source.Controller.Id && f.DestinationControllerId == source.Controller.Id)
            .Select(f => f.OperationId).Distinct().Count();
    }
}
