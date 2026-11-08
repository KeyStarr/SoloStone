using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class StewardoftheVale
    {
        internal const int DbfId = 129837;
        [StandardCard("BE_851", DbfId)]
        internal static void Configure(Card card) => AssembleRules.Configure(card);

        [OfflinePool("BE_851", DbfId)]
        internal static PoolRequirement[] DeclarePools() => new[] {
            new PoolRequirement("assemble-catalog", AssembleRules.Pool, new[] { "DRUID" }, destination: PoolDestination.Other)
        };
    }
}
