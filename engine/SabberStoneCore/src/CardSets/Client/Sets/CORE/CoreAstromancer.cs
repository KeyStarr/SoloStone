using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class CoreAstromancer
    {
        internal const int DbfId = 129846;
        [StandardCard("CORE_BOT_256", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                var ctx = new EffectContext(g,c.Id,s.Id,stack: new TaskStack { Number = c.HandZone.Count });
                if (c.BoardZone.IsFull) return;
                var chosen = ctx.PickRandomCard(B14H4CorePools.Minion(), PoolCostBinding.StackNumber, DbfId);
                if (chosen != null) ctx.Summon(chosen.Id, s.Zone == c.BoardZone ? ((IPlayable)s).ZonePosition + 1 : -1);
            }) };
        }
    }
}
