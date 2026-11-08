using System.Collections.Generic;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SleetStorm
    {
        internal const int DbfId = 123410;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } });
            // Defile uses the same death-processing boundary between damage waves.
            card.Power = new Power { PowerTask = ComplexTask.Create(
                new DamageTask(2, EntityType.TARGET, true),
                new CustomTask((g, c, s, t, stack) => g.DeathProcessingAndAuraUpdate()),
                new RandomTask(1, EntityType.OP_MINIONS), new DamageTask(1, EntityType.STACK, true)) };
        }
    }
}
