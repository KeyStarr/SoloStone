using System.Collections.Generic;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WeaverOfTheCycle
    {
        internal const int DbfId = 114373;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(EmeraldDreamConditions.HoldingExpensiveSpell);
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 } });
            card.TargetingAvailabilityPredicate = (c, definition) => EmeraldDreamConditions.HoldingExpensiveSpell(c);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (t != null && EmeraldDreamConditions.HoldingExpensiveSpell((IPlayable)s))
                    new DamageTask(3, EntityType.TARGET).Process(g, c, s, t, stack);
            }) };
        }
    }
}
