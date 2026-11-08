using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TimewayWarden
    {
        internal const int DbfId = 120988;
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                PowerTask = new CustomTask((g, c, s, t, stack) => {
                    if (!(t is Minion prisoner) || prisoner.Controller == c || prisoner.IsDormant) return;
                    if (!DormantRules.Make(prisoner, 10000, "TIME_442e", s)) return;
                    var link = PayloadRules.StoreCard(s, 1, prisoner.Card);
                    link[PayloadRules.Link] = prisoner.Id;
                    link[PayloadRules.Generation] = DormantRules.SleepGeneration(prisoner);
                }),
                DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                    foreach (var link in PayloadRules.Entries(s, 1))
                        if (g.IdEntityDic.TryGetValue(link[PayloadRules.Link], out var entity) && entity is Minion prisoner
                            && prisoner.IsDormant && DormantRules.SleepGeneration(prisoner) == link[PayloadRules.Generation])
                            DormantRules.Awaken(prisoner);
                })
            };
            card.SetPlayRequirements(new System.Collections.Generic.Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_ENEMY_TARGET, 0 }
            });
        }
        internal static void ConfigurePrison(Card card) { card.Implemented = true; card.Power = new Power(); }
    }
}
