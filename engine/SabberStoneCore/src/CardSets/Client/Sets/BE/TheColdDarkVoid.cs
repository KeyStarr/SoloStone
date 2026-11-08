using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Collections.Generic;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TheColdDarkVoid
    {
        internal const int DbfId = 130314;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            // Snapshot neighbors before damage; deaths resolve after this task, as for Meteor.
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!(t is Minion)) return;
                var neighbors = new TaskStack();
                new IncludeAdjacentTask(EntityType.TARGET).Process(g, c, s, t, neighbors);
                new DamageTask(4, EntityType.TARGET, true).Process(g, c, s, t, stack);
                new DamageTask(2, EntityType.STACK, true).Process(g, c, s, t, neighbors);
            }) };
        }
    }
}
