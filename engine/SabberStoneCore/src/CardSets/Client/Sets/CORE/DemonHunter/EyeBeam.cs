using System.Collections.Generic;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EyeBeam
    {
        internal const int DbfId = 69740;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }
            });
            card.Power = new Power {
                Aura = new AdaptiveCostEffect(p => 1, EffectOperator.SET, new SelfCondition(p =>
                    p.Zone == p.Controller.HandZone && (p.ZonePosition == 0 || p.ZonePosition == SabberStoneCore.Actions.CostRules.HandCountForCost(p) - 1))),
                PowerTask = new DamageTask(3, EntityType.TARGET, true)
            };
        }
    }
}
