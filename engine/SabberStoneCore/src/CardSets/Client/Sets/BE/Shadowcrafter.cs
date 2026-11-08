using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Shadowcrafter
    {
        internal const int DbfId = 129944;
        [StandardCard("BE_702", DbfId)]
        internal static void Configure(Card card) => AssembleRules.Configure(card);

        [OfflinePool("BE_702", DbfId)]
        internal static PoolRequirement[] DeclarePools() => new[] {
            new PoolRequirement("assemble-catalog", AssembleRules.Pool, new[] { "ROGUE" }, destination: PoolDestination.Other)
        };
    }
}
