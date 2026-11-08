using System.Collections.Generic;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DrinkBlood
    {
        internal const int DbfId = 126254;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.Power = new Power { PowerTask = ComplexTask.Create(new DamageTask(3, EntityType.TARGET, true), new CustomTask((g, c, s, t, stack) => {
                c.Hero.HeroPower.IsExhausted = false;
                c.HeroPowerActivationsThisTurn = 0;
            })) };
        }
    }
}
