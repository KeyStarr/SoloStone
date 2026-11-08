using System.Collections.Generic;
using SabberStoneCore.Enums;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class LightningRod
    {
        internal const int DbfId = 120173;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_FRIENDLY_TARGET, 0 }
            });
            card.Power = new Power { PowerTask = ComplexTask.Create(
                new DamageTask(2, EntityType.TARGET, spellDmg: true),
                new RandomTask(1, EntityType.OP_MINIONS),
                new DamageTask(4, EntityType.STACK, spellDmg: true)) };
        }
    }
}
