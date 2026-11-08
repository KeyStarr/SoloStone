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
    internal static class CoreFeralRage
    {
        [StandardCard("CORE_OG_047", 69688)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
        }
        [StandardCard("OG_047a", 38332)]
        internal static void Configure0(Card card) => card.Implemented = true;
        [StandardCard("OG_047b", 38333)]
        internal static void Configure1(Card card) => card.Implemented = true;
        [StandardCard("OG_047e", 38331)]
        internal static void Configure2(Card card) => card.Implemented = true;
    }
}
