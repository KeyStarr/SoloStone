using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Sandcrafter
    {
        internal const int DbfId = 131294;
        [StandardCard("BE_229", DbfId)]
        internal static void Configure(Card card) => AssembleRules.Configure(card);

        [OfflinePool("BE_229", DbfId)]
        internal static PoolRequirement[] DeclarePools() => new[] {
            new PoolRequirement("assemble-catalog", AssembleRules.Pool, new[] { "PALADIN" }, destination: PoolDestination.Other)
        };
    }
}
