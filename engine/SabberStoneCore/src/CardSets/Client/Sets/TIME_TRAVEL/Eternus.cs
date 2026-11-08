using System.Collections.Generic;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Eternus
    {
        [StandardCard("TIME_435", 120543)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_ENEMY_TARGET, 0 } });
            card.SourceTargetingPredicate = (source, target) => target.Health <= ((Minion)source).Health;
            card.Power = new Power { PowerTask = new ControlTask(EntityType.TARGET) };
        }
    }
}
