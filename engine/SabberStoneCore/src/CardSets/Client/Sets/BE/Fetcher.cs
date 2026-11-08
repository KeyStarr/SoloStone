using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Fetcher
    {
        internal const int DbfId = 128892;
        [StandardCard("BE_044", DbfId)]
        internal static void Configure(Card card) => AssembleRules.Configure(card);

        [OfflinePool("BE_044", DbfId)]
        internal static PoolRequirement[] DeclarePools() => new[] {
            new PoolRequirement("assemble-catalog", AssembleRules.Pool, new[] { "NEUTRAL" }, destination: PoolDestination.Other)
        };
    }
}
