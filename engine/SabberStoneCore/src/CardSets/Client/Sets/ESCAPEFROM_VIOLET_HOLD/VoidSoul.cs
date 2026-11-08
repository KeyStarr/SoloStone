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
    internal static class VoidSoul
    {
        internal const int DbfId = 126662;
        private static OfflinePoolFilter Filter() => OfflinePoolFilter.Random(type: CardType.MINION, race: Race.DEMON);
        internal static PoolRequirement[] DeclarePools() => new[] { new PoolRequirement("void-demon-tier", Filter(), new[] { "DEMONHUNTER" },
            PoolCostBinding.StackNumber, 1, allowThinExactCost: true, destination: PoolDestination.Summon) };
        private static int Tier(IPlayable soul) => Math.Min(10, 1 + soul.Game.HistoryFacts.Query.Casts().Count(c =>
            c.ControllerId == soul.Controller.Id && c.DefinitionKnown && c.DbfId == DbfId && c.Outcome == HistoryCastOutcome.Completed));
        [StandardCard("JAIL_732", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, Tier);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var work = new TaskStack { Number = Tier((IPlayable)s) };
                if (c.BoardZone.IsFull) return;
                var context = new EffectContext(g, c.Id, s.Id, stack: work);
                var chosen = context.PickRandomCard(Filter(), PoolCostBinding.StackNumber, DbfId);
                if (chosen != null) context.Summon(chosen.Id);
            }) };
        }
    }
}
