using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using SabberStoneCore.CardSets.Client;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CorpseFarm
    {
        internal const int DbfId = 127535;
        private static OfflinePoolFilter Filter() => OfflinePoolFilter.Random(type: CardType.MINION);
        internal static PoolRequirement[] DeclarePools() => new[] { new PoolRequirement("corpses-exact-cost", Filter(), new[] { "DEATHKNIGHT" },
            PoolCostBinding.StackNumber, 8, allowThinExactCost: true, destination: PoolDestination.Summon) };
        [StandardCard("CORE_WW_374", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new System.Collections.Generic.Dictionary<PlayReq, int> { { PlayReq.REQ_NUM_MINION_SLOTS, 1 } });
            card.PoweredUpWhen(CanSpend);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var work = new TaskStack { Number = CorpseRules.SpendUpTo(c, 8) };
                if (c.BoardZone.IsFull) return;
                var context = new EffectContext(g, c.Id, s.Id, stack: work);
                var chosen = context.PickRandomCard(Filter(), PoolCostBinding.StackNumber, DbfId);
                if (chosen != null) context.Summon(chosen.Id);
            }) };
        }
        private static bool CanSpend(IPlayable source) => source.Game.EnableCorpses && CorpseRules.Available(source.Controller) > 0;
    }
}
