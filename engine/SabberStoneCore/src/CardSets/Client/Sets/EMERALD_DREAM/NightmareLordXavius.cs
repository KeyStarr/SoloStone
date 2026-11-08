using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class NightmareLordXavius
    {
        internal const int DbfId = 115139;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/EDR_856/v1",
                new DiscoverEntitiesWithDarkGiftThenContinueTask(
                    c => DarkGifts.SampleDeckOffers(c, p => p is Minion), recipeDbfId: DbfId)) };
        }
    }
}
