using SabberStoneCore.Actions;
using System.Collections.Generic;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enums;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WindpeakWyrm
    {
        internal const int DbfId = 117714;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } });
            card.Power = new Power {
                Aura = new AdaptiveCostEffect(p => KindredRules.HandDiscount(p, 2)),
                PowerTask = ComplexTask.Create(new DamageTask(5, EntityType.TARGET), new ArmorTask(5))
            };
        }
    }
}
