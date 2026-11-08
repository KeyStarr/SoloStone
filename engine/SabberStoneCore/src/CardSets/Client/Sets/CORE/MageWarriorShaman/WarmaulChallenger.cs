using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Collections.Generic;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class WarmaulChallenger
    {
        internal const int DbfId = 127474;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 },
                { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_ENEMY_TARGET, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!(s is Minion body) || !(t is Minion defender)) return;
                for (int i = 0; i < 30; i++)
                {
                    if (body.IsDead || body.Zone != c.BoardZone || body.Untouchable || defender.IsDead
                        || defender.Zone != c.Opponent.BoardZone || defender.Untouchable) break;
                    new AttackTask(EntityType.SOURCE, EntityType.TARGET).Process(g, c, s, defender);
                }
            }) };
        }
    }
}
