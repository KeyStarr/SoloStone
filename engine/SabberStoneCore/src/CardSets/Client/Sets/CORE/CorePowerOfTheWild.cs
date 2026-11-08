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
    internal static class CorePowerOfTheWild
    {
        [StandardCard("CORE_EX1_160", 69593)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
        }
        [StandardCard("EX1_160a", 60)]
        internal static void Configure0(Card card) => card.Implemented = true;
        [StandardCard("EX1_160b", 835)]
        internal static void Configure1(Card card) => card.Implemented = true;
        [StandardCard("EX1_160t", 812)]
        internal static void Configure2(Card card) => card.Implemented = true;
        [StandardCard("EX1_160be", 980)]
        internal static void Configure3(Card card) => card.Implemented = true;
    }
}
