using System.Collections.Generic;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class HungeringCavern
    {
        internal const int DbfId = 130729;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(BEHighlightConditions.FriendlyMinionDiedThisTurn);
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } });
            card.Power = new Power { Aura = new AdaptiveCostEffect(p => 0, EffectOperator.SET,
                new SelfCondition(BEHighlightConditions.FriendlyMinionDiedThisTurn)),
                PowerTask = new DamageTask(2, EntityType.TARGET, true) };
        }
    }
}
