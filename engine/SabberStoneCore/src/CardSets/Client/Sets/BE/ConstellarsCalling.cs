using System.Collections.Generic;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ConstellarsCalling
    {
        internal const int DbfId = 129788;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_FRIENDLY_TARGET, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (t == null) return;
                stack ??= new TaskStack();
                stack.Number = t.Cost;
                var ctx = new EffectContext(g, c.Id, s.Id, t.Id, stack);
                var chosen = ctx.PickRandomCard(BEPools.Minion(), PoolCostBinding.StackNumber, DbfId);
                if (chosen != null) ctx.Summon(chosen.Id, s is Minion source && source.Zone == c.BoardZone ? source.ZonePosition + 1 : c.BoardZone.SlotCount);
            }) };
        }
    }
}
