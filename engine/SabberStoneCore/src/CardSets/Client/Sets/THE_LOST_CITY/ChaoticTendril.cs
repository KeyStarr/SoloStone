using System;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ChaoticTendril
    {
        private const GameTag Progress=(GameTag)33903;
        internal static OfflinePoolFilter Pool => OfflinePoolFilter.Random(type:CardType.SPELL);
        private static int Display(IPlayable source) => Math.Min(10,1+source.Controller[Progress]);
        [StandardCard("YOG_514",101021)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1,Display);
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=>{
                int tier=Math.Min(10,1+c[Progress]);
                var context=new EffectContext(g,c.Id,s.Id,stack:new TaskStack { Number=tier });
                var spell=context.PickRandomCard(Pool,PoolCostBinding.StackNumber,101021);
                if (spell!=null) AutoPlayRules.Play(Entity.FromCard(c,spell),s);
                c[Progress]=Math.Min(9,c[Progress]+1);
            }) };
        }
    }
}

namespace SabberStoneCore.CardSets.Client
{
    internal static class ChaoticTendrilPools
    {
        [OfflinePool("YOG_514",101021)]
        internal static PoolRequirement[] Pools() => new[] { new PoolRequirement("tendril-spell-tier",Sets.ChaoticTendril.Pool,new[] { "NEUTRAL" },
            PoolCostBinding.StackNumber,1,allowThinExactCost:true, destination: PoolDestination.Other) };
    }
}
