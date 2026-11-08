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
    internal static class CoreLivingRoots
    {
        [StandardCard("CORE_AT_037", 76289)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 } });
        }
        [StandardCard("AT_037a", 2789)]
        internal static void Configure0(Card card) => card.Implemented = true;
        [StandardCard("AT_037b", 2791)]
        internal static void Configure1(Card card) => card.Implemented = true;
        [StandardCard("AT_037t", 2790)]
        internal static void Configure2(Card card) => card.Implemented = true;
    }
}
