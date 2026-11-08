using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DarkestDepths
    {
        internal const int DbfId = 129804;
        [StandardCard("BE_087", DbfId)]
        internal static void Configure(Card card) => AssembleRules.Configure(card);

        [OfflinePool("BE_087", DbfId)]
        internal static PoolRequirement[] DeclarePools() => new[] {
            new PoolRequirement("assemble-catalog", AssembleRules.Pool, new[] { "WARLOCK" }, destination: PoolDestination.Other)
        };

        [StandardCard("BE_087t", 130665)]
        internal static void ConfigureGuard(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
    }
}
