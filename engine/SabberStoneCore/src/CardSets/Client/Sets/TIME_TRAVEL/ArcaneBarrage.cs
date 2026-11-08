using System.Collections.Generic;
using SabberStoneCore.Enums;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ArcaneBarrage
    {
        internal const int DbfId = 119528;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_ENEMY_TARGET, 0 }
            });
            card.Power = new Power { PowerTask = ComplexTask.Create(
                new DamageTask(3, EntityType.TARGET, spellDmg: true),
                new RandomTask(2, EntityType.ENEMIES_NOTARGET),
                new DamageTask(2, EntityType.STACK, spellDmg: true)) };
        }
    }
}
