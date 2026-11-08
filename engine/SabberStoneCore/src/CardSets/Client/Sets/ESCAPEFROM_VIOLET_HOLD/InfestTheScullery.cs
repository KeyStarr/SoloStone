using System.Linq;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class InfestTheScullery
    {
        internal const int DbfId = 127815;
        [StandardCard("JAIL_200", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.ConsumesHistory = true;
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, Bucket);
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                var ctx = new EffectContext(g,c.Id,s.Id,stack:new TaskStack { Number=Bucket((IPlayable)s) });
                for(int i=0;i<2;i++)
                {
                    if(c.BoardZone.IsFull) continue;
                    var chosen=ctx.PickRandomCard(B14H4VioletPools.Minion(),PoolCostBinding.StackNumber,DbfId);
                    if(chosen!=null)ctx.Summon(chosen.Id);
                }
            }) };
        }
        // Unpinned attack qualification and ceiling: forced/zero-damage combats count; cancelled proposals do not; no cap/fallback.
        private static int Bucket(IPlayable s) => 2+s.Game.HistoryFacts.Query.AttacksFinished()
            .Where(f=>f.ControllerId==s.Controller.Id && f.AttackerType==CardType.HERO && f.ReachedCombat)
            .Select(f=>f.AttackId).Distinct().Count();
    }
}
