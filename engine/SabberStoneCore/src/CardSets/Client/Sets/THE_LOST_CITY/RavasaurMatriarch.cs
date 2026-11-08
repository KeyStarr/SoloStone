using System.Collections.Generic;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RavasaurMatriarch
    {
        internal const int DbfId = 117598;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_ENEMY_TARGET, 0 }
            });
            card.TargetingAvailabilityPredicate = (controller, definition) => controller.HasKindredMatch(definition);
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                int repeats = SabberStoneCore.Actions.KindredRules.BeginMatchedClause((IPlayable)s);
                for (int i = 0; i < repeats && t != null; i++)
                    new DamageTask(((Minion)s).AttackDamage, EntityType.TARGET).Process(g, c, s, t, stack);
            }) };
        }
    }
}
