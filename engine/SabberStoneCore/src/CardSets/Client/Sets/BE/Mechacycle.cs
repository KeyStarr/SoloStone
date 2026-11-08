using System;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Mechacycle
    {
        internal const int DbfId = 132177;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var budget = new TaskStack { Number = 4 };
                var ctx = new EffectContext(g, c.Id, s.Id, stack: budget);
                int position = EffectContext.DeathrattlePosition(c, s);
                while (budget.Number > 0 && !c.BoardZone.IsFull)
                {
                    var chosen = ctx.PickRandomCard(BEPools.Mech(), PoolCostBinding.StackNumberMaxCost, DbfId);
                    if (chosen == null) break;
                    int summoned = ctx.Summon(chosen.Id, Math.Min(position, c.BoardZone.SlotCount));
                    if (summoned == 0) break;
                    budget.Number -= chosen.Cost;
                    g.DeathProcessingAndAuraUpdate();
                    var minion = g.IdEntityDic[summoned];
                    position = minion.Zone == c.BoardZone ? minion.ZonePosition + 1 : Math.Min(position, c.BoardZone.SlotCount);
                }
            }) };
        }
    }
}
