using System;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class LifeCycle
    {
        internal const int DbfId = 117655;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }
            });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!(t is Minion target) || target.Zone != target.Controller.BoardZone) return;
                var owner = target.Controller;
                int position = target.ZonePosition, cost = target.Card.Cost;
                new DestroyTask(EntityType.TARGET, forcedDeathPhase: true).Process(g, c, s, target, stack);
                stack ??= new TaskStack();
                stack.Number = cost;
                var ctx = new EffectContext(g, owner.Id, s.Id, stack: stack);
                var chosen = ctx.PickRandomCard(TheLostCityPools.LifeCycleMinion(), PoolCostBinding.StackNumber, DbfId);
                if (chosen == null || owner.BoardZone.IsFull) return;
                var replacement = (Minion)Entity.FromCard(owner, chosen,
                    new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                // SummonTask clamps to the source's board; the target may belong to the opponent.
                Generic.SummonBlock(g, replacement, Math.Min(position, owner.BoardZone.SlotCount), s);
            }) };
        }
    }
}
