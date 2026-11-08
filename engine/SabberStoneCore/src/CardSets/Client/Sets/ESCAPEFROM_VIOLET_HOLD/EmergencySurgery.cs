using System.Collections.Generic;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class EmergencySurgery
    {
        internal const int DbfId = 126911;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 },
                { PlayReq.REQ_ENEMY_TARGET, 0 }, { PlayReq.REQ_NUM_MINION_SLOTS, 1 }
            });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!(t is Minion target) || target.Zone != c.Opponent.BoardZone || target.ToBeDestroyed) return;
                var ctx = new EffectContext(g, c.Id, s.Id, t.Id, stack);
                var nurses = new List<int>();
                for (int i = 0; i < 4; i++)
                {
                    int nurse = ctx.Summon("JAIL_454t");
                    if (nurse != 0) nurses.Add(nurse);
                }
                // Reserve all available summon slots before any attack can free one.
                foreach (int id in nurses)
                {
                    if (target.Zone != c.Opponent.BoardZone || target.ToBeDestroyed) break;
                    var nurse = (Minion)g.IdEntityDic[id];
                    if (nurse.Zone != c.BoardZone || nurse.ToBeDestroyed) continue;
                    new AttackTask(EntityType.SOURCE, EntityType.TARGET).Process(g, c, nurse, target);
                }
            }) };
        }
    }
}
