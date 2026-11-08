using System;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PluckyPodling
    {
        internal const int DbfId = 115295;
        internal static OfflinePoolFilter Filter() => OfflinePoolFilter.Random(type: CardType.MINION);
        internal static PoolRequirement[] DeclarePools() => new[] { new PoolRequirement("podling-destination", Filter(),
            new[] { "SHAMAN" }, PoolCostBinding.StackNumber, 2, allowThinExactCost: true, destination: PoolDestination.Transform) };

        [StandardCard("EDR_529", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            TransformRules.Register(card.Id, (source, intended) => {
                int cost = intended.Cost >= 10 ? intended.Cost : Math.Min(10, intended.Cost + 2);
                return new EffectContext(source.Game, source.Controller.Id, source.Id, stack: new TaskStack { Number = cost })
                    .PickRandomCard(Filter(), PoolCostBinding.StackNumber, DbfId);
            });
        }
    }
}

namespace SabberStoneCore.CardSets.Client
{
    internal static class B17PluckyPodlingPools
    {
        [OfflinePool("EDR_529", 115295)]
        internal static PoolRequirement[] DeclarePools() => Sets.PluckyPodling.DeclarePools();
    }
}
