using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using System.Collections.Generic;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TREEEES
    {
        internal const int DbfId = 117646;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!(t is Minion defender)) return;
                var ctx = new EffectContext(g, c.Id, s.Id);
                var created = new List<int>();
                for (int i = 0; i < 4; i++)
                {
                    int id = ctx.Summon("TLC_230t");
                    if (id != 0) created.Add(id);
                    g.DeathProcessingAndAuraUpdate();
                }
                foreach (int id in created)
                {
                    if (defender.IsDead || defender.Zone?.Type != Zone.PLAY || defender.Untouchable) break;
                    var attacker = (Minion)g.IdEntityDic[id];
                    if (attacker.IsDead || attacker.Zone != c.BoardZone || attacker.Untouchable) continue;
                    new AttackTask(EntityType.STACK, EntityType.TARGET).Process(g, c, s, defender,
                        new TaskStack { Playables = new[] { attacker } });
                }
            }) };
        }
        internal static void ConfigureToken(Card card) { card.Implemented = true; }
    }
}
