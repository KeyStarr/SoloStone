using System.Collections.Generic;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class FogsailFreebooter
    {
        [StandardCard("CS3_022", 66854)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 } });
            card.TargetingAvailabilityPredicate = (c, _) => c.Hero.Weapon != null;
            card.SourceTargetingPredicate = (source, _) => Armed(source);
            card.PoweredUpWhen(Armed);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (c.Hero.Weapon != null && t != null) new DamageTask(2, EntityType.TARGET).Process(g, c, s, t, stack);
            }) };
        }
        private static bool Armed(IPlayable source) => source.Controller.Hero.Weapon != null;
    }
}
