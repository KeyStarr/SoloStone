using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Waveshaping
    {
        internal const int DbfId = 120746;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("client/TIME_701/v1",
                new DiscoverThenContinueTask(DiscoverEntitySource.Deck, DiscoverEntityAction.Draw, RejectedOfferAction.Bottom)) };
        }
    }
}
