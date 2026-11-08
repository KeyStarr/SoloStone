using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Loader;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CoreFlipperFriends
    {
        [StandardCard("CORE_TSC_650", 117037)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
        }
        [StandardCard("TSC_650a", 74953)]
        internal static void ConfigureOrca(Card card)
        {
            card.Implemented = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_NUM_MINION_SLOTS, 1 } });
            card.Power = new Power { PowerTask = new SummonTask("TSC_650t") };
        }
        [StandardCard("TSC_650d", 74960)]
        internal static void ConfigureOtters(Card card)
        {
            card.Implemented = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_NUM_MINION_SLOTS, 1 } });
            card.Power = new Power { PowerTask = new SummonTask("TSC_650t4", 6) };
        }
        [StandardCard("TSC_650t", 73463)]
        internal static void Configure0(Card card) => card.Implemented = true;
        [StandardCard("TSC_650t4", 73553)]
        internal static void Configure1(Card card) => card.Implemented = true;
    }
}
