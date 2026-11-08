using System.Collections.Generic;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SpecterSpecialist
    {
        internal const int DbfId = 127065;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_FRIENDLY_TARGET, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (t == null) return;
                if (t[GameTag.REBORN] == 1)
                    new SummonCopyTask(EntityType.TARGET, side: s is Minion m && m.Zone == c.BoardZone ? SummonSide.RIGHT : SummonSide.DEFAULT).Process(g, c, s, t, stack);
                else {
                    t[GameTag.REBORN] = 1;
                    t[GameTag.HAS_BEEN_REBORN] = 0;
                }
            }) };
        }
    }
}
