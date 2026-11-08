using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CultistMap
    {
        [StandardCard("TLC_515", 117697)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.ConsumesHistory = true;
            card.Power = new Power { PowerTask = new DecisionSpellTask("b16/cultist-map/v1",
                new DiscoverThenContinueTask(DiscoverEntitySource.Deck, DiscoverEntityAction.Draw)) };
        }
        [StandardCard("TLC_515e", 121756)]
        internal static void ConfigureWatch(Card card) => ConfigureMapWatch(card);
        internal static void ConfigureMapWatch(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = StateTaskList.Suspendable("b16/" + card.Id + "/follow-up",
                new DiscoverThenContinueTask(DiscoverRealRules.RetainedOffers, DiscoverRealRules.PrepareRetained),
                new CustomTask(DiscoverRealRules.DeliverRetained)) };
        }
    }
}
