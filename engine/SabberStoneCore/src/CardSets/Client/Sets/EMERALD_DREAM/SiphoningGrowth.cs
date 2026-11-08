using System.Collections.Generic;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SiphoningGrowth
    {
        internal const int DbfId = 116684;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_FRIENDLY_TARGET, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                // Armor is conditional on the sacrifice, including an extra body with a departed target.
                if (!(t is Minion) || t.Zone != c.BoardZone || t.ToBeDestroyed) return;
                new DestroyTask(EntityType.TARGET).Process(g,c,s,t,stack);
                new ArmorTask(8).Process(g,c,s,t,stack);
            }) };
        }
    }
}
